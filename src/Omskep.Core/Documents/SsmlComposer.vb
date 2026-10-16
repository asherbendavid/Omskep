Option Strict On
Option Explicit On
Option Infer On

Imports System.Globalization
Imports System.Text.RegularExpressions
Imports System.Xml
Imports System.Xml.Linq

Namespace Documents

    ''' <summary>
    ''' Result of composing a document into the SSML actually sent to Azure. Immutable.
    ''' </summary>
    Public NotInheritable Class CompositionResult

        Public ReadOnly Property IsSuccess As Boolean
        Public ReadOnly Property Ssml As String
        ''' <summary>User-facing explanation. Nothing when IsSuccess is True.</summary>
        Public ReadOnly Property FailureReason As String

        Private Sub New(isSuccess As Boolean, ssml As String, failureReason As String)
            Me.IsSuccess = isSuccess
            Me.Ssml = ssml
            Me.FailureReason = failureReason
        End Sub

        Friend Shared Function Success(ssml As String) As CompositionResult
            Return New CompositionResult(True, ssml, Nothing)
        End Function

        Friend Shared Function Failure(reason As String) As CompositionResult
            Return New CompositionResult(False, Nothing, reason)
        End Function

    End Class

    ''' <summary>
    ''' Builds the SSML actually sent to Azure from the editor's document text. Pure, Critical: export
    ''' is only as safe as this is correct. The saved document is never touched -- every change here
    ''' (whitespace collapse, prosody wrappers) exists only in the string this returns.
    '''
    ''' Steps, in order:
    ''' 1. The document must already be well-formed (the editor's own check); composing a malformed
    '''    document is refused rather than guessed at.
    ''' 2. Anything outside the single root element -- the omskep-meta metadata block, any other
    '''    comment or processing instruction -- is stripped. Only the root element and its contents
    '''    are ever sent to Azure.
    ''' 3. Structural rule: no &lt;voice&gt; may be nested inside another &lt;voice&gt;. Not an XML
    '''    well-formedness question -- a project rule, enforced here.
    ''' 4. Whitespace in text content is collapsed to a single space (spike evidence: a bare line break
    '''    with no sentence-ending punctuation near it is sometimes heard as a pause/restart; a single
    '''    space between words never is). Tags and attribute values are untouched.
    ''' 5. Each &lt;voice&gt; whose locale (the first two hyphen-separated parts of its name, e.g.
    '''    "he-IL" from "he-IL-HilaNeural") has an entry in speakingRatePercent gets its content wrapped
    '''    in a &lt;prosody rate="±N%"&gt; element. A voice with no matching entry is left alone.
    ''' 6. The result is re-checked for well-formedness as a defensive final step; composing a malformed
    '''    document from a well-formed one would be a bug here, not a user error.
    ''' Omskep never adds spoken text of its own at any step.
    ''' </summary>
    Public NotInheritable Class SsmlComposer

        Private Sub New()
        End Sub

        Public Shared Function Compose(documentText As String, speakingRatePercent As IReadOnlyDictionary(Of String, Integer)) As CompositionResult
            Dim editorCheck As CheckResult = WellFormednessChecker.Check(documentText)
            If Not editorCheck.IsWellFormed Then
                Return CompositionResult.Failure("The document is not well-formed: " & editorCheck.StatusText)
            End If

            Dim doc As XDocument
            Try
                doc = XDocument.Parse(documentText, LoadOptions.SetLineInfo)
            Catch ex As XmlException
                ' Should not happen: WellFormednessChecker just approved this text. Defensive only.
                Return CompositionResult.Failure("The document could not be parsed for export: " & ex.Message)
            End Try

            If doc.Root Is Nothing Then
                Return CompositionResult.Failure("The document has no content.")
            End If

            ' Anything outside the root element -- the omskep-meta block (docs/omskep-meta-spec.md), any
            ' other comment or processing instruction, a stray XML declaration -- never reaches Azure.
            ' doc.ToString() below would otherwise serialize these verbatim; they are deliberately stripped
            ' here, after parsing, rather than by scanning the raw text, so whatever shape the prolog/epilog
            ' actually took (however many comments or PIs, in whatever order) is already correctly resolved
            ' by the parse above.
            For Each topLevelNode As XNode In doc.Nodes().ToList()
                If Not ReferenceEquals(topLevelNode, doc.Root) Then
                    topLevelNode.Remove()
                End If
            Next

            Dim ns As XNamespace = doc.Root.Name.Namespace

            Dim nestedVoiceReason As String = FindNestedVoice(doc, ns)
            If nestedVoiceReason IsNot Nothing Then
                Return CompositionResult.Failure(nestedVoiceReason)
            End If

            CollapseWhitespace(doc)
            ApplyProsodyRates(doc, ns, speakingRatePercent)

            Dim composed As String = doc.ToString(SaveOptions.DisableFormatting)

            Dim finalCheck As CheckResult = WellFormednessChecker.Check(composed)
            If Not finalCheck.IsWellFormed Then
                Return CompositionResult.Failure("Composition produced a malformed document (please report this): " & finalCheck.StatusText)
            End If

            Return CompositionResult.Success(composed)
        End Function

        ''' <summary>Nothing when no <voice> contains another <voice>; otherwise a user-facing message naming the line.</summary>
        Private Shared Function FindNestedVoice(doc As XDocument, ns As XNamespace) As String
            For Each outer As XElement In doc.Descendants(ns + "voice")
                Dim inner As XElement = outer.Descendants(ns + "voice").FirstOrDefault()
                If inner IsNot Nothing Then
                    Dim info As IXmlLineInfo = inner
                    Dim where As String = If(info.HasLineInfo(), " (line " & info.LineNumber.ToString(CultureInfo.InvariantCulture) & ")", String.Empty)
                    Return "A <voice> element is nested inside another <voice> element" & where & ". Close the outer <voice> before opening a new one."
                End If
            Next
            Return Nothing
        End Function

        ''' <summary>Collapses every run of whitespace in text content to a single space. Tags and attributes are untouched.</summary>
        Private Shared Sub CollapseWhitespace(doc As XDocument)
            For Each node As XText In doc.DescendantNodes().OfType(Of XText)()
                node.Value = Regex.Replace(node.Value, "\s+", " ")
            Next
        End Sub

        ''' <summary>Wraps the content of each <voice> in <prosody rate="..."> when its locale has a preset. Mutates doc.</summary>
        Private Shared Sub ApplyProsodyRates(doc As XDocument, ns As XNamespace, speakingRatePercent As IReadOnlyDictionary(Of String, Integer))
            If speakingRatePercent Is Nothing OrElse speakingRatePercent.Count = 0 Then Return

            ' Materialize first: we mutate each voice's children while iterating the voice list itself.
            For Each voice As XElement In doc.Descendants(ns + "voice").ToList()
                Dim voiceName As String = CStr(voice.Attribute("name")?.Value)
                Dim locale As String = LocaleOf(voiceName)
                If locale Is Nothing Then Continue For

                Dim ratePercent As Integer
                If Not speakingRatePercent.TryGetValue(locale, ratePercent) Then Continue For

                Dim children As List(Of XNode) = voice.Nodes().ToList()
                For Each child As XNode In children
                    child.Remove()
                Next
                Dim prosody As New XElement(ns + "prosody", New XAttribute("rate", FormatRate(ratePercent)))
                prosody.Add(children)
                voice.Add(prosody)
            Next
        End Sub

        ''' <summary>The locale prefix of a voice short name, e.g. "he-IL" from "he-IL-HilaNeural". Nothing if unparseable.</summary>
        Private Shared Function LocaleOf(voiceName As String) As String
            If String.IsNullOrWhiteSpace(voiceName) Then Return Nothing
            Dim parts As String() = voiceName.Split("-"c)
            If parts.Length < 2 Then Return Nothing
            Return parts(0) & "-" & parts(1)
        End Function

        Private Shared Function FormatRate(percent As Integer) As String
            Dim sign As String = If(percent >= 0, "+", String.Empty)
            Return sign & percent.ToString(CultureInfo.InvariantCulture) & "%"
        End Function

    End Class

End Namespace
