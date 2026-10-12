Option Strict On
Option Explicit On
Option Infer On

Imports System.Globalization
Imports System.Text.RegularExpressions
Imports System.Xml
Imports System.Xml.Linq

Namespace Documents

    ''' <summary>One standalone, send-ready SSML document plus its estimated spoken duration.</summary>
    Public NotInheritable Class DocumentChunk
        Public ReadOnly Property Ssml As String
        Public ReadOnly Property EstimatedSeconds As Double

        Public Sub New(ssml As String, estimatedSeconds As Double)
            Me.Ssml = ssml
            Me.EstimatedSeconds = estimatedSeconds
        End Sub
    End Class

    ''' <summary>Result of chunking a composed document. Immutable.</summary>
    Public NotInheritable Class ChunkingResult
        Public ReadOnly Property IsSuccess As Boolean
        Public ReadOnly Property Chunks As IReadOnlyList(Of DocumentChunk)
        ''' <summary>User-facing explanation. Nothing when IsSuccess is True.</summary>
        Public ReadOnly Property FailureReason As String

        Private Sub New(isSuccess As Boolean, chunks As IReadOnlyList(Of DocumentChunk), failureReason As String)
            Me.IsSuccess = isSuccess
            Me.Chunks = chunks
            Me.FailureReason = failureReason
        End Sub

        Friend Shared Function Success(chunks As IReadOnlyList(Of DocumentChunk)) As ChunkingResult
            Return New ChunkingResult(True, chunks, Nothing)
        End Function

        Friend Shared Function Failure(reason As String) As ChunkingResult
            Return New ChunkingResult(False, Nothing, reason)
        End Function
    End Class

    ''' <summary>
    ''' Splits an already-composed document (SsmlComposer's output) into standalone chunks, each under
    ''' the duration cap. Pure, Critical. Input is assumed well-formed with no nested &lt;voice&gt;
    ''' (SsmlComposer already guarantees both); this does not re-check them.
    '''
    ''' Two split granularities, both resolved by the same greedy packer (GreedyPack):
    ''' - Primary: whole top-level &lt;voice&gt; elements are grouped together until the next one would
    '''   cross the cap ("free" splits -- each chunk gets complete, unmodified &lt;voice&gt; elements).
    ''' - Escape valve: only when a single &lt;voice&gt; element's own estimated duration alone exceeds
    '''   the cap, its content is split at sentence ends (never inside a user's &lt;sub&gt;,
    '''   &lt;phoneme&gt; or nested &lt;prosody&gt;, which are treated as indivisible). If no sentence
    '''   end is found before the cap even then, it is reported, not forced through a worse split.
    '''
    ''' Duration estimate: characters/10 seconds, scaled by the enclosing &lt;prosody rate&gt; if
    ''' present. A numeric percent (e.g. "-20%") is parsed exactly; anything else (a keyword rate like
    ''' "slow", or anything unparseable) is left untouched in the output but estimated using a fixed
    ''' conservative multiplier (matches the phase 0 measurement of "slow") rather than assuming no
    ''' slowdown at all.
    ''' </summary>
    Public NotInheritable Class Chunker

        Public Const DefaultCapSeconds As Double = 480

        ''' <summary>Sum of BillableCharacterCounter.CountUpperBound over every chunk's Ssml -- the number to show
        ''' before export (the editor's own status-bar count is a lower figure by design: it excludes anything
        ''' composition adds, such as a prosody wrapper). Pure; callable before any synthesis is attempted.</summary>
        Public Shared Function TotalBillableCharacters(chunks As IReadOnlyList(Of DocumentChunk)) As Integer
            If chunks Is Nothing Then Return 0
            Dim total As Integer = 0
            For Each chunk As DocumentChunk In chunks
                total += BillableCharacterCounter.CountUpperBound(chunk.Ssml)
            Next
            Return total
        End Function

        ''' <summary>Phase 0 spike measured "slow" at about x1.55 real duration. Used whenever a rate
        ''' cannot be read as a numeric percent, so the estimate stays conservative instead of silently
        ''' assuming no slowdown. The rate text itself is always copied through to the chunk unchanged.</summary>
        Private Const UnknownRateDurationMultiplier As Double = 1.55

        Private Shared ReadOnly SentenceEnd As New Regex("[.?!]\s+(?=\p{Lu})", RegexOptions.Compiled)

        Private Sub New()
        End Sub

        Public Shared Function Chunk(composedSsml As String, Optional capSeconds As Double = DefaultCapSeconds) As ChunkingResult
            Dim doc As XDocument
            Try
                doc = XDocument.Parse(composedSsml, LoadOptions.SetLineInfo)
            Catch ex As XmlException
                ' Should not happen: the caller is expected to pass SsmlComposer's own output. Defensive only.
                Return ChunkingResult.Failure("The composed document could not be parsed for chunking: " & ex.Message)
            End Try
            If doc.Root Is Nothing Then Return ChunkingResult.Failure("The document has no content.")

            Dim ns As XNamespace = doc.Root.Name.Namespace
            Dim voices As List(Of XElement) = doc.Root.Elements(ns + "voice").ToList()
            If voices.Count = 0 Then Return ChunkingResult.Failure("The document has no <voice> content to export.")

            Dim chunks As New List(Of DocumentChunk)
            Dim pending As New List(Of XElement)
            Dim pendingSeconds As Double = 0

            For Each voice As XElement In voices
                Dim multiplier As Double = MultiplierFor(voice, ns)
                Dim seconds As Double = BillableCharacterCounter.CountUpperBound(voice.ToString(SaveOptions.DisableFormatting)) / 10.0 * multiplier

                If seconds > capSeconds Then
                    If pending.Count > 0 Then
                        chunks.Add(BuildWholeVoiceChunk(doc.Root, pending, pendingSeconds))
                        pending = New List(Of XElement)()
                        pendingSeconds = 0
                    End If
                    Dim split As ChunkingResult = SplitOversizedVoice(doc.Root, voice, ns, multiplier, capSeconds)
                    If Not split.IsSuccess Then Return split
                    chunks.AddRange(split.Chunks)
                    Continue For
                End If

                If pending.Count > 0 AndAlso pendingSeconds + seconds > capSeconds Then
                    chunks.Add(BuildWholeVoiceChunk(doc.Root, pending, pendingSeconds))
                    pending = New List(Of XElement)()
                    pendingSeconds = 0
                End If
                pending.Add(voice)
                pendingSeconds += seconds
            Next

            If pending.Count > 0 Then chunks.Add(BuildWholeVoiceChunk(doc.Root, pending, pendingSeconds))

            Return ChunkingResult.Success(chunks)
        End Function

        ' ---- whole-voice-element ("free split") path ----

        Private Shared Function BuildWholeVoiceChunk(root As XElement, voiceGroup As List(Of XElement), totalSeconds As Double) As DocumentChunk
            Dim speakClone As New XElement(root.Name, root.Attributes().Select(Function(a) New XAttribute(a)))
            For Each v As XElement In voiceGroup
                speakClone.Add(New XElement(v))
            Next
            Return New DocumentChunk(New XDocument(speakClone).ToString(SaveOptions.DisableFormatting), totalSeconds)
        End Function

        ''' <summary>Reads the locale's current prosody rate multiplier for duration estimation. 1.0 if the voice has no prosody wrapper.</summary>
        Private Shared Function MultiplierFor(voice As XElement, ns As XNamespace) As Double
            Dim prosody As XElement = voice.Elements(ns + "prosody").FirstOrDefault()
            If prosody Is Nothing Then Return 1.0
            Dim rateAttr As XAttribute = prosody.Attribute("rate")
            If rateAttr Is Nothing Then Return 1.0

            Dim rateText As String = rateAttr.Value.Trim()
            If rateText.EndsWith("%", StringComparison.Ordinal) Then
                Dim percent As Double
                If Double.TryParse(rateText.Substring(0, rateText.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, percent) Then
                    Dim denominator As Double = 1.0 + percent / 100.0
                    If denominator > 0.01 Then Return 1.0 / denominator
                End If
            End If
            Return UnknownRateDurationMultiplier
        End Function

        ' ---- inside-one-voice ("reconstructed split") path ----

        ''' <summary>Private Class and Shared Function kept together: a single voice's content, split at sentence
        ''' ends into one or more standalone chunks, or a failure if no split point exists below the cap.</summary>
        Private Shared Function SplitOversizedVoice(root As XElement, voice As XElement, ns As XNamespace, multiplier As Double, capSeconds As Double) As ChunkingResult
            Dim prosody As XElement = voice.Elements(ns + "prosody").FirstOrDefault()
            Dim container As XElement = If(prosody, voice)

            Dim units As List(Of AtomicUnit) = BuildAtomicUnits(container, multiplier)
            Dim packed As PackResult = GreedyPack(units, capSeconds)
            If Not packed.Succeeded Then
                Dim voiceName As String = If(voice.Attribute("name")?.Value, "(unnamed)")
                Return ChunkingResult.Failure(
                    "A stretch of text in <voice name=""" & voiceName & """> has no sentence end and is longer than the " &
                    capSeconds.ToString("0", CultureInfo.InvariantCulture) & "-second cap. It starts: """ & packed.FailingUnit.Preview() &
                    """. Find that in the editor and break it up by hand -- a voice switch or added punctuation both work.")
            End If

            Dim results As New List(Of DocumentChunk)
            For Each group As List(Of AtomicUnit) In packed.Groups
                Dim chunkVoice As New XElement(ns + "voice", voice.Attributes().Select(Function(a) New XAttribute(a)))
                Dim target As XElement = chunkVoice
                If prosody IsNot Nothing Then
                    Dim prosodyClone As New XElement(ns + "prosody", prosody.Attributes().Select(Function(a) New XAttribute(a)))
                    chunkVoice.Add(prosodyClone)
                    target = prosodyClone
                End If

                Dim totalSeconds As Double = 0
                For Each unit As AtomicUnit In group
                    target.Add(unit.Materialize())
                    totalSeconds += unit.EstimatedSeconds
                Next

                Dim speakClone As New XElement(root.Name, root.Attributes().Select(Function(a) New XAttribute(a)))
                speakClone.Add(chunkVoice)
                results.Add(New DocumentChunk(New XDocument(speakClone).ToString(SaveOptions.DisableFormatting), totalSeconds))
            Next
            Return ChunkingResult.Success(results)
        End Function

        ''' <summary>Flattens one container's direct content into atomic, indivisible units: a text node is cut
        ''' at each sentence end it contains; any other node (sub, phoneme, a nested prosody, a comment) is kept whole.</summary>
        Private Shared Function BuildAtomicUnits(container As XElement, multiplier As Double) As List(Of AtomicUnit)
            Dim list As New List(Of AtomicUnit)
            For Each node As XNode In container.Nodes()
                Dim text As XText = TryCast(node, XText)
                If text IsNot Nothing Then
                    For Each fragment As String In SplitIntoSentenceFragments(text.Value)
                        list.Add(AtomicUnit.ForText(fragment, multiplier))
                    Next
                    Continue For
                End If

                Dim element As XElement = TryCast(node, XElement)
                If element IsNot Nothing Then
                    ' Measure a namespace-stripped copy, not the live element: an inline element like <sub>
                    ' inherits its namespace from an ancestor rather than declaring its own, so serializing
                    ' it alone (out of that context) makes .NET inject a redundant xmlns just for this one
                    ' call -- inflating the count well past what actually ends up in the final document,
                    ' where the namespace is declared exactly once, on <speak>.
                    Dim billable As Integer = BillableCharacterCounter.CountUpperBound(StripNamespace(element).ToString(SaveOptions.DisableFormatting))
                    list.Add(AtomicUnit.ForOpaque(element, billable, multiplier))
                    Continue For
                End If

                ' Rare (a comment or similar) at this depth; kept whole, never split.
                list.Add(AtomicUnit.ForOpaque(node, node.ToString().Length, multiplier))
            Next
            Return list
        End Function

        ''' <summary>A detached, namespace-free copy for measurement only (never for the real output): avoids
        ''' .NET injecting a redundant xmlns when an inherited-namespace element is serialized on its own.</summary>
        Private Shared Function StripNamespace(node As XNode) As XNode
            Dim element As XElement = TryCast(node, XElement)
            If element IsNot Nothing Then
                Return New XElement(element.Name.LocalName,
                                     element.Attributes().Where(Function(a) Not a.IsNamespaceDeclaration),
                                     element.Nodes().Select(Function(n) StripNamespace(n)))
            End If
            Dim text As XText = TryCast(node, XText)
            If text IsNot Nothing Then Return New XText(text.Value)
            Dim comment As XComment = TryCast(node, XComment)
            If comment IsNot Nothing Then Return New XComment(comment.Value)
            Return New XText(node.ToString())
        End Function

        ''' <summary>Splits text at each "sentence end" (., ? or ! followed by whitespace and an upper-case letter).
        ''' A sentence end spanning a node boundary (e.g. ending right before a &lt;sub&gt;) is not detected --
        ''' the pieces simply stay packed together unless some other boundary is needed. With no match, returns
        ''' the whole text as a single fragment (no internal split point exists).</summary>
        Private Shared Function SplitIntoSentenceFragments(text As String) As List(Of String)
            Dim fragments As New List(Of String)
            Dim previous As Integer = 0
            For Each m As Match In SentenceEnd.Matches(text)
                Dim boundary As Integer = m.Index + m.Length
                fragments.Add(text.Substring(previous, boundary - previous))
                previous = boundary
            Next
            If previous < text.Length Then fragments.Add(text.Substring(previous))
            If fragments.Count = 0 Then fragments.Add(text)
            Return fragments
        End Function

        ' ---- shared greedy packer ----

        Private NotInheritable Class AtomicUnit
            Public ReadOnly Property EstimatedSeconds As Double
            Private ReadOnly _text As String
            Private ReadOnly _node As XNode

            Private Sub New(text As String, node As XNode, estimatedSeconds As Double)
                _text = text
                _node = node
                Me.EstimatedSeconds = estimatedSeconds
            End Sub

            Public Shared Function ForText(fragment As String, multiplier As Double) As AtomicUnit
                Return New AtomicUnit(fragment, Nothing, fragment.Length / 10.0 * multiplier)
            End Function

            Public Shared Function ForOpaque(node As XNode, billableChars As Integer, multiplier As Double) As AtomicUnit
                Return New AtomicUnit(Nothing, node, billableChars / 10.0 * multiplier)
            End Function

            ''' <summary>A fresh, detached copy ready to add to a reconstructed chunk.</summary>
            Public Function Materialize() As XNode
                If _text IsNot Nothing Then Return New XText(_text)
                Dim element As XElement = TryCast(_node, XElement)
                If element IsNot Nothing Then Return New XElement(element)
                Dim comment As XComment = TryCast(_node, XComment)
                If comment IsNot Nothing Then Return New XComment(comment.Value)
                Return New XText(_node.ToString())
            End Function

            ''' <summary>A short, human-searchable excerpt for a failure message. A composed document is a single
            ''' XML line throughout (SsmlComposer removes every newline), so a line number cannot locate anything
            ''' here -- this excerpt is meant to be found with the editor's own Find instead.</summary>
            Public Function Preview() As String
                Const MaxLength As Integer = 60
                Dim raw As String = If(_text, _node.ToString()).Trim()
                If raw.Length <= MaxLength Then Return raw
                Return raw.Substring(0, MaxLength) & "…"
            End Function
        End Class

        Private NotInheritable Class PackResult
            Public ReadOnly Property Succeeded As Boolean
            Public ReadOnly Property Groups As List(Of List(Of AtomicUnit))
            Public ReadOnly Property FailingUnit As AtomicUnit

            Private Sub New(succeeded As Boolean, groups As List(Of List(Of AtomicUnit)), failingUnit As AtomicUnit)
                Me.Succeeded = succeeded
                Me.Groups = groups
                Me.FailingUnit = failingUnit
            End Sub

            Public Shared Function Ok(groups As List(Of List(Of AtomicUnit))) As PackResult
                Return New PackResult(True, groups, Nothing)
            End Function

            Public Shared Function Fail(failingUnit As AtomicUnit) As PackResult
                Return New PackResult(False, Nothing, failingUnit)
            End Function
        End Class

        ''' <summary>Greedily groups units so each group's total stays at or under capSeconds. Fails only when a
        ''' single unit alone exceeds the cap -- there is nothing finer left to split it into.</summary>
        Private Shared Function GreedyPack(units As List(Of AtomicUnit), capSeconds As Double) As PackResult
            Dim groups As New List(Of List(Of AtomicUnit))
            Dim current As New List(Of AtomicUnit)
            Dim currentSeconds As Double = 0

            For Each u As AtomicUnit In units
                If u.EstimatedSeconds > capSeconds Then Return PackResult.Fail(u)
                If current.Count > 0 AndAlso currentSeconds + u.EstimatedSeconds > capSeconds Then
                    groups.Add(current)
                    current = New List(Of AtomicUnit)()
                    currentSeconds = 0
                End If
                current.Add(u)
                currentSeconds += u.EstimatedSeconds
            Next
            If current.Count > 0 Then groups.Add(current)
            Return PackResult.Ok(groups)
        End Function

    End Class

End Namespace