Namespace Secrets

    ''' <summary>What counts as a usable Azure key. Messages never contain the key.</summary>
    Public Module KeyRules

        ''' <summary>Nothing if the key is acceptable, otherwise a plain message saying why not.</summary>
        Public Function Problem(key As String) As String
            Dim cleaned = If(key, String.Empty).Trim()
            If cleaned.Length = 0 Then Return "A key is required."
            For Each c In cleaned
                If Char.IsWhiteSpace(c) OrElse Char.IsControl(c) Then Return "A key cannot contain spaces or line breaks."
            Next
            Return Nothing
        End Function

    End Module

End Namespace
