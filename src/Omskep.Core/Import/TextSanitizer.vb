Option Strict On
Option Explicit On
Option Infer On

Imports System.Text
Imports System.Text.RegularExpressions

Namespace Import

    ''' <summary>
    ''' What <see cref="TextSanitizer.Sanitize"/> produced, plus a count of everything it
    ''' changed, so the UI can tell the user exactly what was done (nothing is silent).
    ''' </summary>
    Public NotInheritable Class SanitizeResult

        ''' <summary>Cleaned text, LF line endings, with &amp; &lt; &gt; escaped. Safe to place
        ''' between SSML tags as it is. Contains no SSML scaffolding.</summary>
        Public ReadOnly Property Body As String

        ''' <summary>Private-use characters that were removed, by code point, with counts.
        ''' Includes supplementary-plane private-use characters.</summary>
        Public ReadOnly Property RemovedPrivateUse As IReadOnlyDictionary(Of Integer, Integer)

        ''' <summary>Characters that cannot appear in XML and were removed: control
        ''' characters other than tab and line breaks, lone surrogates, U+FFFE and U+FFFF.</summary>
        Public ReadOnly Property RemovedInvalidCharacters As Integer

        ''' <summary>Quote characters before a standalone "n" that were changed to
        ''' <see cref="TextSanitizer.ArticleQuote"/>.</summary>
        Public ReadOnly Property FixedArticleQuotes As Integer

        ''' <summary>Lines that had trailing spaces or tabs removed.</summary>
        Public ReadOnly Property TrimmedLines As Integer

        Public Sub New(body As String,
                       removedPrivateUse As IReadOnlyDictionary(Of Integer, Integer),
                       removedInvalidCharacters As Integer,
                       fixedArticleQuotes As Integer,
                       trimmedLines As Integer)
            Me.Body = body
            Me.RemovedPrivateUse = removedPrivateUse
            Me.RemovedInvalidCharacters = removedInvalidCharacters
            Me.FixedArticleQuotes = fixedArticleQuotes
            Me.TrimmedLines = trimmedLines
        End Sub

        ''' <summary>Total number of private-use characters removed.</summary>
        Public ReadOnly Property RemovedPrivateUseTotal As Integer
            Get
                Dim total As Integer = 0
                For Each kv In RemovedPrivateUse
                    total += kv.Value
                Next
                Return total
            End Get
        End Property

    End Class

    ''' <summary>
    ''' Turns raw extracted or pasted text into text that is safe to place inside an SSML
    ''' document. Pure and stateless. Same path for PDF extraction and paste-from-Word.
    ''' Never removes words: it only fixes characters (see the pipeline in
    ''' <see cref="Sanitize"/>) and reports what it did.
    ''' </summary>
    Public NotInheritable Class TextSanitizer

        ''' <summary>
        ''' The form the Afrikaans article "n" is normalised to. Word's autocorrect produces
        ''' a wrong-direction opening quote (U+2018) before it. Straight apostrophe is
        ''' the document's own majority form. If it does not read well aloud, change this
        ''' one constant (for example to ChrW(&amp;H2019)): every form is then normalised to it.
        ''' </summary>
        Public Const ArticleQuote As String = "'"

        ' A quote of any form, directly before a standalone "n" (not part of a longer
        ' word, and not preceded by a letter).
        Private Shared ReadOnly ArticleQuoteRegex As New Regex(
            "(?<!\p{L})['\u2018\u2019]n(?!\p{L})", RegexOptions.CultureInvariant)

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Pipeline, in this order (the order matters):
        ''' 1. One scan: CR and CRLF become LF; vertical tab and form feed become LF; private-use
        '''    characters, other control characters, lone surrogates, U+FFFE and U+FFFF are
        '''    removed and counted. This also guarantees step 2 cannot throw.
        ''' 2. Unicode NFC (turns Hebrew presentation forms such as U+FB4B into their
        '''    base letter plus point, and composes letters such as e + U+0308).
        ''' 3. Quote before a standalone "n" becomes <see cref="ArticleQuote"/>.
        ''' 4. Trailing spaces and tabs are trimmed on every line (interior runs are
        '''    left alone: wide runs are a boilerplate signal that is flagged, not removed).
        ''' 5. Only &amp;, &lt; and &gt; are escaped. No other character is re-encoded.
        ''' </summary>
        Public Shared Function Sanitize(raw As String) As SanitizeResult
            Dim privateUse As New SortedDictionary(Of Integer, Integer)()

            If String.IsNullOrEmpty(raw) Then
                Return New SanitizeResult(String.Empty, privateUse, 0, 0, 0)
            End If

            ' Step 1
            Dim invalidCount As Integer = 0
            Dim sb As New StringBuilder(raw.Length)
            Dim i As Integer = 0
            While i < raw.Length
                Dim c As Char = raw(i)
                If Char.IsHighSurrogate(c) Then
                    If i + 1 < raw.Length AndAlso Char.IsLowSurrogate(raw(i + 1)) Then
                        Dim pairCode As Integer = Char.ConvertToUtf32(c, raw(i + 1))
                        If IsPrivateUse(pairCode) Then
                            Tally(privateUse, pairCode)
                        Else
                            sb.Append(c).Append(raw(i + 1))
                        End If
                        i += 2
                    Else
                        invalidCount += 1
                        i += 1
                    End If
                ElseIf Char.IsLowSurrogate(c) Then
                    invalidCount += 1
                    i += 1
                Else
                    Dim code As Integer = AscW(c)
                    If IsPrivateUse(code) Then
                        Tally(privateUse, code)
                    ElseIf code = 13 Then
                        ' CR: a CRLF pair yields one LF (from the LF that follows).
                        If Not (i + 1 < raw.Length AndAlso raw(i + 1) = ChrW(10)) Then sb.Append(ChrW(10))
                    ElseIf code = 11 OrElse code = 12 Then
                        sb.Append(ChrW(10))
                    ElseIf (code < 32 AndAlso code <> 9 AndAlso code <> 10) OrElse code = &HFFFE OrElse code = &HFFFF Then
                        invalidCount += 1
                    Else
                        sb.Append(c)
                    End If
                    i += 1
                End If
            End While

            ' Step 2
            Dim work As String = sb.ToString().Normalize(NormalizationForm.FormC)

            ' Step 3
            Dim fixedCount As Integer = 0
            Dim evaluator As MatchEvaluator =
                Function(m As Match) As String
                    If m.Value.Chars(0) <> ArticleQuote.Chars(0) Then fixedCount += 1
                    Return ArticleQuote & "n"
                End Function
            work = ArticleQuoteRegex.Replace(work, evaluator)

            ' Step 4
            Dim lines As String() = work.Split(ChrW(10))
            Dim trimmedCount As Integer = 0
            For idx As Integer = 0 To lines.Length - 1
                Dim stripped As String = lines(idx).TrimEnd(" "c, ChrW(9))
                If stripped.Length <> lines(idx).Length Then
                    trimmedCount += 1
                    lines(idx) = stripped
                End If
            Next
            work = String.Join(ChrW(10), lines)

            ' Step 5 (ampersand first, so the entities written here are not escaped again)
            work = work.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")

            Return New SanitizeResult(work, privateUse, invalidCount, fixedCount, trimmedCount)
        End Function

        Private Shared Function IsPrivateUse(codePoint As Integer) As Boolean
            Return (codePoint >= &HE000 AndAlso codePoint <= &HF8FF) OrElse
                   (codePoint >= &HF0000 AndAlso codePoint <= &HFFFFD) OrElse
                   (codePoint >= &H100000 AndAlso codePoint <= &H10FFFD)
        End Function

        Private Shared Sub Tally(counts As SortedDictionary(Of Integer, Integer), codePoint As Integer)
            Dim existing As Integer = 0
            counts.TryGetValue(codePoint, existing)
            counts(codePoint) = existing + 1
        End Sub

    End Class

End Namespace
