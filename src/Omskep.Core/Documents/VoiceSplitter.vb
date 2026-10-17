Option Strict On
Option Explicit On
Option Infer On

Imports System.Globalization

Namespace Documents

    ''' <summary>
    ''' Result of a <see cref="VoiceSplitter.Split"/> call. Immutable, same shape as
    ''' <see cref="CompositionResult"/>/<see cref="TextSearchResult"/>: on success, <see cref="Edits"/> is
    ''' meant to be applied through <c>SsmlEditor.ApplyEdits(edits, NewText)</c> so the edit is one undo
    ''' step and self-verifies; <see cref="NewText"/> is the expected resulting document, computed the
    ''' same way for an independent check.
    ''' </summary>
    Public NotInheritable Class VoiceSplitResult

        Public ReadOnly Property IsSuccess As Boolean
        Public ReadOnly Property NewText As String
        Public ReadOnly Property Edits As IReadOnlyList(Of TextEdit)
        ''' <summary>User-facing explanation. Nothing when IsSuccess is True.</summary>
        Public ReadOnly Property FailureReason As String

        Private Sub New(isSuccess As Boolean, newText As String, edits As IReadOnlyList(Of TextEdit), failureReason As String)
            Me.IsSuccess = isSuccess
            Me.NewText = newText
            Me.Edits = edits
            Me.FailureReason = failureReason
        End Sub

        Friend Shared Function Success(newText As String, edits As IReadOnlyList(Of TextEdit)) As VoiceSplitResult
            Return New VoiceSplitResult(True, newText, edits, Nothing)
        End Function

        Friend Shared Function Failure(reason As String) As VoiceSplitResult
            Return New VoiceSplitResult(False, Nothing, Array.Empty(Of TextEdit)(), reason)
        End Function

    End Class

    ''' <summary>
    ''' Splits the enclosing &lt;voice&gt; around a selection, or renames the enclosing &lt;voice&gt; when
    ''' the selection covers its whole (trimmed) content. Pure, Critical: a bug here can corrupt the
    ''' document structurally or lose text. Catalog-agnostic by design (confirmed with the developer) --
    ''' this function trusts whatever voice name string it is given; checking that the name actually
    ''' exists on the current key/region is the voice-catalog builder's job, not this one's.
    '''
    ''' Rules, in order:
    ''' 1. Basic input validation (null document, out-of-range selection, bad voice name) is refused,
    '''    never guessed at.
    ''' 2. The document must already be well-formed (reuses WellFormednessChecker, the same gate
    '''    SsmlComposer uses).
    ''' 3. A pre-existing hand-typed nested &lt;voice&gt; anywhere in the document refuses the whole
    '''    operation -- this function never tries to "fix around" an already rule-violating document.
    ''' 4. A selection touching the document header (anything before the root element -- the
    '''    omskep-meta block and its processing instruction) is refused unconditionally, before any
    '''    other branch logic runs.
    ''' 5. A selection must be entirely inside one &lt;voice&gt; element's content; spanning two or more
    '''    is refused.
    ''' 6. A selection whose start or end falls strictly inside a &lt;sub&gt;, &lt;phoneme&gt; or
    '''    &lt;prosody&gt; element is refused (never auto-repaired, per the developer's call).
    ''' 7. Whitespace-only gaps between a selection edge and its voice's content edge are absorbed into
    '''    the split, never left behind as an empty/whitespace-only &lt;voice&gt; and never discarded --
    '''    every character is preserved somewhere in the output.
    ''' 8. An empty selection (caret only) resolves to "the enclosing &lt;voice&gt;" and is always a
    '''    rename, never a split.
    ''' Omskep never adds spoken text of its own at any step; every edit here only ever touches tags.
    ''' </summary>
    Public NotInheritable Class VoiceSplitter

        Private Sub New()
        End Sub

        Public Shared Function Split(documentText As String, selectionStart As Integer, selectionLength As Integer, newVoiceName As String) As VoiceSplitResult
            If documentText Is Nothing Then
                Return VoiceSplitResult.Failure("There is no document to edit.")
            End If
            If selectionStart < 0 OrElse selectionLength < 0 OrElse selectionStart + selectionLength > documentText.Length Then
                Return VoiceSplitResult.Failure("The selection is outside the document.")
            End If

            Dim trimmedName As String = If(newVoiceName, String.Empty).Trim()
            If trimmedName.Length = 0 Then
                Return VoiceSplitResult.Failure("No voice name was given.")
            End If
            If trimmedName.IndexOfAny(New Char() {"<"c, "&"c, """"c}) >= 0 Then
                Return VoiceSplitResult.Failure("The voice name contains a character (<, &, or "") that cannot appear in an attribute value, so it is refused rather than escaped.")
            End If

            Dim check As CheckResult = WellFormednessChecker.Check(documentText)
            If Not check.IsWellFormed Then
                Return VoiceSplitResult.Failure("The document is not well-formed: " & check.StatusText)
            End If

            Dim scan As ScanResult
            Try
                scan = Scanner.Scan(documentText)
            Catch
                ' Defensive only: WellFormednessChecker just approved this text, so the scanner's own
                ' narrower assumptions (proper tag/quote matching) should always hold here.
                Return VoiceSplitResult.Failure("The document could not be analysed for voice splitting (please report this).")
            End Try

            If scan.RootStart < 0 Then
                Return VoiceSplitResult.Failure("The document has no root element.")
            End If
            If scan.MaxVoiceDepth > 1 Then
                Return VoiceSplitResult.Failure("The document already contains a <voice> element nested inside another <voice> element" & scan.NestedVoiceLocationText & ". Fix that before splitting voices.")
            End If
            If scan.Voices.Count = 0 Then
                Return VoiceSplitResult.Failure("The document has no <voice> element to split.")
            End If
            If selectionStart < scan.RootStart Then
                Return VoiceSplitResult.Failure("The selection is inside the document header, not the spoken body.")
            End If

            Dim selectionEnd As Integer = selectionStart + selectionLength

            If selectionLength = 0 Then
                Dim caretVoice As VoiceSpan = FindContainingVoice(scan.Voices, selectionStart, selectionStart)
                If caretVoice Is Nothing Then
                    Return VoiceSplitResult.Failure("The caret is not inside a <voice> element.")
                End If
                Return RenameVoice(documentText, caretVoice, trimmedName)
            End If

            Dim voice As VoiceSpan = FindContainingVoice(scan.Voices, selectionStart, selectionEnd)
            If voice Is Nothing Then
                Return VoiceSplitResult.Failure("The selection is not entirely inside a single <voice> element.")
            End If

            For Each inline As ElementSpan In scan.InlineElements
                If inline.TagOpenStart < voice.TagOpenStart OrElse inline.TagCloseEnd > voice.TagCloseEnd Then Continue For
                If inline.TagOpenStart < selectionStart AndAlso selectionStart < inline.TagCloseEnd Then
                    Return VoiceSplitResult.Failure("The selection starts inside a <" & inline.Name & "> element. Move the selection edge outside it first.")
                End If
                If inline.TagOpenStart < selectionEnd AndAlso selectionEnd < inline.TagCloseEnd Then
                    Return VoiceSplitResult.Failure("The selection ends inside a <" & inline.Name & "> element. Move the selection edge outside it first.")
                End If
            Next

            Dim leadingGap As String = documentText.Substring(voice.ContentStart, selectionStart - voice.ContentStart)
            Dim trailingGap As String = documentText.Substring(selectionEnd, voice.ContentEnd - selectionEnd)
            Dim effectiveStart As Integer = If(leadingGap.Trim().Length = 0, voice.ContentStart, selectionStart)
            Dim effectiveEnd As Integer = If(trailingGap.Trim().Length = 0, voice.ContentEnd, selectionEnd)

            Dim hasLeadingFragment As Boolean = effectiveStart > voice.ContentStart
            Dim hasTrailingFragment As Boolean = effectiveEnd < voice.ContentEnd

            If Not hasLeadingFragment AndAlso Not hasTrailingFragment Then
                Return RenameVoice(documentText, voice, trimmedName)
            End If

            Dim edits As New List(Of TextEdit)()

            If hasLeadingFragment Then
                edits.Add(New TextEdit(effectiveStart, 0, "</voice><voice name=""" & trimmedName & """>"))
            Else
                ' No leading fragment survives in the original voice, so there is nothing to close before
                ' the new one -- relabel the voice's own opening tag instead of inserting a new element.
                edits.Add(New TextEdit(voice.NameValueStart, voice.NameValueLength, trimmedName))
            End If

            If hasTrailingFragment Then
                edits.Add(New TextEdit(effectiveEnd, 0, "</voice><voice name=""" & voice.Name & """>"))
            End If
            ' Else: nothing to add at the end -- the original closing tag now simply closes the new voice.

            Dim newText As String = ApplyLocally(documentText, edits)
            Return VoiceSplitResult.Success(newText, edits)
        End Function

        Private Shared Function RenameVoice(documentText As String, voice As VoiceSpan, newName As String) As VoiceSplitResult
            Dim edits As New List(Of TextEdit) From {New TextEdit(voice.NameValueStart, voice.NameValueLength, newName)}
            Return VoiceSplitResult.Success(ApplyLocally(documentText, edits), edits)
        End Function

        ''' <summary>The voice whose content fully contains [start, end], inclusive both edges -- or Nothing
        ''' if no single voice does (the selection spans a boundary, or sits in the gap between voices).</summary>
        Private Shared Function FindContainingVoice(voices As IReadOnlyList(Of VoiceSpan), start As Integer, [end] As Integer) As VoiceSpan
            For Each v As VoiceSpan In voices
                If v.ContentStart <= start AndAlso [end] <= v.ContentEnd Then Return v
            Next
            Return Nothing
        End Function

        ''' <summary>Applies a small set of non-overlapping edits (against original offsets) to produce the
        ''' resulting text, for VoiceSplitResult.NewText. Mirrors the shape SsmlEditor.ApplyEdits itself
        ''' uses, so the two can be compared/verified independently.</summary>
        Private Shared Function ApplyLocally(documentText As String, edits As IReadOnlyList(Of TextEdit)) As String
            Dim ordered As List(Of TextEdit) = edits.OrderBy(Function(e) e.Start).ToList()
            Dim sb As New Text.StringBuilder(documentText.Length + 64)
            Dim copiedTo As Integer = 0
            For Each edit As TextEdit In ordered
                sb.Append(documentText, copiedTo, edit.Start - copiedTo)
                sb.Append(edit.NewText)
                copiedTo = edit.Start + edit.Length
            Next
            sb.Append(documentText, copiedTo, documentText.Length - copiedTo)
            Return sb.ToString()
        End Function

        ' ---- scanning ----

        Private NotInheritable Class VoiceSpan
            Public Property Name As String
            Public Property TagOpenStart As Integer
            Public Property ContentStart As Integer
            Public Property ContentEnd As Integer
            Public Property TagCloseEnd As Integer
            Public Property NameValueStart As Integer
            Public Property NameValueLength As Integer
        End Class

        Private NotInheritable Class ElementSpan
            Public Property Name As String
            Public Property TagOpenStart As Integer
            Public Property TagCloseEnd As Integer
        End Class

        Private NotInheritable Class ScanResult
            Public Property RootStart As Integer = -1
            Public Property MaxVoiceDepth As Integer = 0
            Public Property NestedVoiceLocationText As String = String.Empty
            Public Property Voices As New List(Of VoiceSpan)()
            Public Property InlineElements As New List(Of ElementSpan)()
        End Class

        Private NotInheritable Class OpenTag
            Public Property Name As String
            Public Property TagOpenStart As Integer
            Public Property ContentStart As Integer
            Public Property NameValueStart As Integer
            Public Property NameValueLength As Integer
        End Class

        ''' <summary>
        ''' A minimal, hand-rolled tag scanner -- not a general XML parser. It only needs to walk a
        ''' document that WellFormednessChecker has already approved, and only needs to recognise a
        ''' narrow, known vocabulary: comments, CDATA, processing instructions, and the open/close/
        ''' self-closing tags for voice/sub/phoneme/prosody (everything else is skipped as plain text or
        ''' an uninteresting tag). It works entirely in character offsets into the original string, so no
        ''' line/column round-trip is needed anywhere in VoiceSplitter.
        ''' </summary>
        Private NotInheritable Class Scanner

            Private Sub New()
            End Sub

            Public Shared Function Scan(text As String) As ScanResult
                Dim result As New ScanResult()
                Dim n As Integer = text.Length
                Dim i As Integer = 0
                Dim openStack As New Stack(Of OpenTag)()
                Dim voiceDepth As Integer = 0

                While i < n
                    If text(i) <> "<"c Then
                        i += 1
                        Continue While
                    End If

                    If Matches(text, i, "<!--") Then
                        Dim closeAt As Integer = text.IndexOf("-->", i + 4, StringComparison.Ordinal)
                        i = If(closeAt < 0, n, closeAt + 3)

                    ElseIf Matches(text, i, "<![CDATA[") Then
                        Dim closeAt As Integer = text.IndexOf("]]>", i + 9, StringComparison.Ordinal)
                        i = If(closeAt < 0, n, closeAt + 3)

                    ElseIf i + 1 < n AndAlso text(i + 1) = "?"c Then
                        Dim closeAt As Integer = text.IndexOf("?>", i + 2, StringComparison.Ordinal)
                        i = If(closeAt < 0, n, closeAt + 2)

                    ElseIf i + 1 < n AndAlso text(i + 1) = "/"c Then
                        Dim tagOpenStart As Integer = i
                        Dim gt As Integer = FindUnquotedGreaterThan(text, i + 2)
                        If gt < 0 Then Throw New InvalidOperationException("Unterminated closing tag.")
                        Dim name As String = text.Substring(i + 2, gt - (i + 2)).Trim()
                        Dim tagCloseEnd As Integer = gt + 1

                        If openStack.Count > 0 AndAlso String.Equals(openStack.Peek().Name, name, StringComparison.Ordinal) Then
                            Dim top As OpenTag = openStack.Pop()
                            Select Case name
                                Case "voice"
                                    voiceDepth -= 1
                                    result.Voices.Add(New VoiceSpan With {
                                        .Name = text.Substring(top.NameValueStart, top.NameValueLength),
                                        .TagOpenStart = top.TagOpenStart,
                                        .ContentStart = top.ContentStart,
                                        .ContentEnd = tagOpenStart,
                                        .TagCloseEnd = tagCloseEnd,
                                        .NameValueStart = top.NameValueStart,
                                        .NameValueLength = top.NameValueLength
                                    })
                                Case "sub", "phoneme", "prosody"
                                    result.InlineElements.Add(New ElementSpan With {
                                        .Name = name,
                                        .TagOpenStart = top.TagOpenStart,
                                        .TagCloseEnd = tagCloseEnd
                                    })
                            End Select
                        End If
                        i = tagCloseEnd

                    Else
                        Dim tagOpenStart As Integer = i
                        Dim gt As Integer = FindUnquotedGreaterThan(text, i + 1)
                        If gt < 0 Then Throw New InvalidOperationException("Unterminated opening tag.")
                        Dim selfClosing As Boolean = gt > 0 AndAlso text(gt - 1) = "/"c
                        Dim nameEnd As Integer = i + 1
                        While nameEnd < n AndAlso Not Char.IsWhiteSpace(text(nameEnd)) AndAlso text(nameEnd) <> ">"c AndAlso text(nameEnd) <> "/"c
                            nameEnd += 1
                        End While
                        Dim name As String = text.Substring(i + 1, nameEnd - (i + 1))
                        Dim tagCloseEnd As Integer = gt + 1

                        If result.RootStart < 0 Then result.RootStart = tagOpenStart

                        Dim nameValueStart As Integer = -1
                        Dim nameValueLength As Integer = 0
                        If name = "voice" Then
                            FindAttributeValue(text, tagOpenStart, gt, "name", nameValueStart, nameValueLength)
                        End If

                        If selfClosing Then
                            Select Case name
                                Case "voice"
                                    result.Voices.Add(New VoiceSpan With {
                                        .Name = If(nameValueStart >= 0, text.Substring(nameValueStart, nameValueLength), String.Empty),
                                        .TagOpenStart = tagOpenStart,
                                        .ContentStart = tagCloseEnd,
                                        .ContentEnd = tagCloseEnd,
                                        .TagCloseEnd = tagCloseEnd,
                                        .NameValueStart = Math.Max(nameValueStart, 0),
                                        .NameValueLength = nameValueLength
                                    })
                                Case "sub", "phoneme", "prosody"
                                    result.InlineElements.Add(New ElementSpan With {.Name = name, .TagOpenStart = tagOpenStart, .TagCloseEnd = tagCloseEnd})
                            End Select
                        Else
                            If name = "voice" Then
                                voiceDepth += 1
                                If voiceDepth > result.MaxVoiceDepth Then result.MaxVoiceDepth = voiceDepth
                                If voiceDepth > 1 AndAlso result.NestedVoiceLocationText.Length = 0 Then
                                    result.NestedVoiceLocationText = " (character " & tagOpenStart.ToString(CultureInfo.InvariantCulture) & ")"
                                End If
                            End If
                            openStack.Push(New OpenTag With {
                                .Name = name,
                                .TagOpenStart = tagOpenStart,
                                .ContentStart = tagCloseEnd,
                                .NameValueStart = Math.Max(nameValueStart, 0),
                                .NameValueLength = nameValueLength
                            })
                        End If
                        i = tagCloseEnd
                    End If
                End While

                Return result
            End Function

            Private Shared Function Matches(text As String, at As Integer, literal As String) As Boolean
                Return at + literal.Length <= text.Length AndAlso String.CompareOrdinal(text, at, literal, 0, literal.Length) = 0
            End Function

            ''' <summary>The index of the first '>' at or after fromIndex that is not inside a quoted
            ''' attribute value. -1 if none is found (should not happen on well-formed input).</summary>
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

            ''' <summary>Finds attrName="value" (or '...') within text(tagStart..tagEnd), requiring the
            ''' attribute name to be preceded by whitespace (or the tag start) so e.g. "surname" never
            ''' matches a search for "name".</summary>
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

    End Class

End Namespace
