Option Strict On
Option Explicit On
Option Infer On

Imports Omskep.Core.Voices

Namespace Documents

    ''' <summary>One locale's voices, in the order a picker should show them. Voices are already sorted
    ''' (by short name) because VoiceCatalog.VoicesFor returns them that way; this class does not re-sort
    ''' them, so gender is left exactly as the catalog ordered it -- unsorted, as agreed.</summary>
    Public NotInheritable Class VoiceLocaleGroup

        Public ReadOnly Property Locale As String
        Public ReadOnly Property Voices As IReadOnlyList(Of VoiceInfo)

        Public Sub New(locale As String, voices As IReadOnlyList(Of VoiceInfo))
            Me.Locale = locale
            Me.Voices = voices
        End Sub

    End Class

    ''' <summary>
    ''' Builds the voice list for the picker, in the agreed order: the document's own default locale (from
    ''' &lt;speak xml:lang&gt;) first, then locales already used elsewhere in the document (alphabetical),
    ''' then every other locale in the catalog (alphabetical). Pure, Important tier (per the reliability
    ''' guide's tiering): a wrong order is a usability annoyance, not data loss, since nothing here ever
    ''' edits the document -- VoiceSplitter and the attribute-insertion edit are what actually touch text,
    ''' and both stay exactly as catalog-agnostic as before.
    ''' </summary>
    Public NotInheritable Class VoicePickerListBuilder

        Private Sub New()
        End Sub

        Public Shared Function Build(documentText As String, catalog As VoiceCatalog) As IReadOnlyList(Of VoiceLocaleGroup)
            Dim result As New List(Of VoiceLocaleGroup)()
            If catalog Is Nothing OrElse catalog.Count = 0 OrElse String.IsNullOrEmpty(documentText) Then Return result

            Dim placed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            Dim defaultLocale As String = CanonicalLocale(catalog, FindDefaultLocale(documentText))
            If defaultLocale IsNot Nothing AndAlso placed.Add(defaultLocale) Then
                result.Add(New VoiceLocaleGroup(defaultLocale, catalog.VoicesFor(defaultLocale)))
            End If

            Dim usedLocales As New List(Of String)()
            For Each locale As String In FindUsedLocales(documentText, catalog)
                Dim canonical As String = CanonicalLocale(catalog, locale)
                If canonical IsNot Nothing Then usedLocales.Add(canonical)
            Next
            usedLocales = usedLocales.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            usedLocales.Sort(StringComparer.OrdinalIgnoreCase)
            For Each locale As String In usedLocales
                If placed.Add(locale) Then result.Add(New VoiceLocaleGroup(locale, catalog.VoicesFor(locale)))
            Next

            ' catalog.Locales is already sorted, so what's left just needs the already-placed ones skipped.
            For Each locale As String In catalog.Locales
                If placed.Add(locale) Then result.Add(New VoiceLocaleGroup(locale, catalog.VoicesFor(locale)))
            Next

            Return result
        End Function

        ''' <summary>Matches a locale string (however it was obtained -- xml:lang, or guessed from a short
        ''' name) to the catalog's own casing, case-insensitively. Nothing if the catalog has no such
        ''' locale at all -- nothing to offer, so the caller skips it rather than inventing an empty group.</summary>
        Private Shared Function CanonicalLocale(catalog As VoiceCatalog, locale As String) As String
            If String.IsNullOrWhiteSpace(locale) Then Return Nothing
            For Each known As String In catalog.Locales
                If String.Equals(known, locale, StringComparison.OrdinalIgnoreCase) Then Return known
            Next
            Return Nothing
        End Function

        ''' <summary>The &lt;speak&gt; element's xml:lang value, or Nothing if there is no root element or
        ''' no such attribute. Lenient: never throws, even on malformed or partial text.</summary>
        Private Shared Function FindDefaultLocale(text As String) As String
            Dim n As Integer = text.Length
            Dim i As Integer = 0
            While i < n
                If text(i) <> "<"c Then
                    i += 1
                    Continue While
                End If
                If Matches(text, i, "<!--") Then
                    Dim closeAt As Integer = text.IndexOf("-->", i + 4, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 3
                ElseIf i + 1 < n AndAlso text(i + 1) = "?"c Then
                    Dim closeAt As Integer = text.IndexOf("?>", i + 2, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 2
                ElseIf i + 1 < n AndAlso (text(i + 1) = "/"c OrElse text(i + 1) = "!"c) Then
                    Exit While ' a close tag or a declaration before any open tag: no root element found
                Else
                    Dim gt As Integer = FindUnquotedGreaterThan(text, i + 1)
                    If gt < 0 Then Exit While
                    Dim valueStart As Integer = -1
                    Dim valueLength As Integer = 0
                    FindAttributeValue(text, i, gt, "xml:lang", valueStart, valueLength)
                    Return If(valueStart >= 0, text.Substring(valueStart, valueLength), Nothing)
                End If
            End While
            Return Nothing
        End Function

        ''' <summary>The locale of every distinct &lt;voice name="..."&gt; used in the document: the
        ''' catalog's own Locale for that short name when known, else a guess from the short name's own
        ''' "ll-CC-..." shape (matching SsmlComposer's own locale convention), skipped entirely if neither
        ''' works. Lenient, like FindDefaultLocale: never throws.</summary>
        Private Shared Function FindUsedLocales(text As String, catalog As VoiceCatalog) As List(Of String)
            Dim locales As New List(Of String)()
            Dim n As Integer = text.Length
            Dim i As Integer = 0
            While i < n
                If text(i) <> "<"c Then
                    i += 1
                    Continue While
                End If
                If Matches(text, i, "<!--") Then
                    Dim closeAt As Integer = text.IndexOf("-->", i + 4, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 3
                ElseIf Matches(text, i, "<![CDATA[") Then
                    Dim closeAt As Integer = text.IndexOf("]]>", i + 9, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 3
                ElseIf i + 1 < n AndAlso text(i + 1) = "?"c Then
                    Dim closeAt As Integer = text.IndexOf("?>", i + 2, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 2
                ElseIf i + 1 < n AndAlso text(i + 1) = "/"c Then
                    Dim gt As Integer = FindUnquotedGreaterThan(text, i + 2)
                    If gt < 0 Then Exit While
                    i = gt + 1
                Else
                    Dim gt As Integer = FindUnquotedGreaterThan(text, i + 1)
                    If gt < 0 Then Exit While
                    Dim nameEnd As Integer = i + 1
                    While nameEnd < n AndAlso Not Char.IsWhiteSpace(text(nameEnd)) AndAlso text(nameEnd) <> ">"c AndAlso text(nameEnd) <> "/"c
                        nameEnd += 1
                    End While
                    If String.Equals(text.Substring(i + 1, nameEnd - (i + 1)), "voice", StringComparison.Ordinal) Then
                        Dim valueStart As Integer = -1
                        Dim valueLength As Integer = 0
                        FindAttributeValue(text, i, gt, "name", valueStart, valueLength)
                        If valueStart >= 0 Then
                            Dim shortName As String = text.Substring(valueStart, valueLength)
                            Dim fromCatalog As VoiceInfo = catalog.TryFind(shortName)
                            If fromCatalog IsNot Nothing Then
                                locales.Add(fromCatalog.Locale)
                            Else
                                Dim guessed As String = GuessLocaleFromShortName(shortName)
                                If guessed IsNot Nothing Then locales.Add(guessed)
                            End If
                        End If
                    End If
                    i = gt + 1
                End If
            End While
            Return locales
        End Function

        ''' <summary>"af-ZA-WillemNeural" -&gt; "af-ZA": the first two hyphen-separated parts, matching
        ''' SsmlComposer's own locale convention. Nothing if the short name doesn't have that shape.</summary>
        Private Shared Function GuessLocaleFromShortName(shortName As String) As String
            Dim parts As String() = shortName.Split("-"c)
            If parts.Length < 3 Then Return Nothing
            Return parts(0) & "-" & parts(1)
        End Function

        Private Shared Function Matches(text As String, at As Integer, literal As String) As Boolean
            Return at + literal.Length <= text.Length AndAlso String.CompareOrdinal(text, at, literal, 0, literal.Length) = 0
        End Function

        Private Shared Function FindUnquotedGreaterThan(text As String, fromIndex As Integer) As Integer
            Dim inQuote As Char = ChrW(0)
            For p As Integer = fromIndex To text.Length - 1
                Dim c As Char = text(p)
                If inQuote <> ChrW(0) Then
                    If c = inQuote Then inQuote = ChrW(0)
                ElseIf c = """"c OrElse c = "'"c Then
                    inQuote = c
                ElseIf c = ">"c Then
                    Return p
                End If
            Next
            Return -1
        End Function

        Private Shared Sub FindAttributeValue(text As String, tagStart As Integer, tagEnd As Integer, attrName As String, ByRef valueStart As Integer, ByRef valueLength As Integer)
            valueStart = -1
            valueLength = 0
            Dim idx As Integer = tagStart
            While idx < tagEnd
                Dim found As Integer = text.IndexOf(attrName, idx, tagEnd - idx, StringComparison.Ordinal)
                If found < 0 Then Exit While
                Dim precededOk As Boolean = found = tagStart OrElse Char.IsWhiteSpace(text(found - 1))
                Dim p As Integer = found + attrName.Length
                While p < tagEnd AndAlso Char.IsWhiteSpace(text(p))
                    p += 1
                End While
                If precededOk AndAlso p < tagEnd AndAlso text(p) = "="c Then
                    p += 1
                    While p < tagEnd AndAlso Char.IsWhiteSpace(text(p))
                        p += 1
                    End While
                    If p < tagEnd AndAlso (text(p) = """"c OrElse text(p) = "'"c) Then
                        Dim quote As Char = text(p)
                        Dim vStart As Integer = p + 1
                        Dim q As Integer = text.IndexOf(quote, vStart)
                        If q >= 0 AndAlso q <= tagEnd Then
                            valueStart = vStart
                            valueLength = q - vStart
                            Return
                        End If
                    End If
                End If
                idx = found + attrName.Length
            End While
        End Sub

    End Class

End Namespace
