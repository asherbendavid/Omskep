Namespace Speech

    ''' <summary>
    ''' The region becomes part of the host name the key is sent to, so it is validated strictly:
    ''' lowercase letters and digits only. "evil.com/" or "x#" must never reach a URL.
    ''' </summary>
    Public Module AzureRegion

        Public Const MaxLength As Integer = 40

        ''' <summary>Trimmed, lowercased region id, or Nothing if it is not a plain region id like "southafricanorth".</summary>
        Public Function Normalize(region As String) As String
            If region Is Nothing Then Return Nothing
            Dim r = region.Trim().ToLowerInvariant()
            If r.Length = 0 OrElse r.Length > MaxLength Then Return Nothing
            For Each c In r
                Dim isLetter = c >= "a"c AndAlso c <= "z"c
                Dim isDigit = c >= "0"c AndAlso c <= "9"c
                If Not (isLetter OrElse isDigit) Then Return Nothing
            Next
            Return r
        End Function

        Public Function IsValid(region As String) As Boolean
            Return Normalize(region) IsNot Nothing
        End Function

    End Module

End Namespace
