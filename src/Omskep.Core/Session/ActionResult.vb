Namespace Session

    ''' <summary>What a Settings action did, in words for the user. Message never contains the key.</summary>
    Public NotInheritable Class ActionResult
        Public ReadOnly Property Success As Boolean
        Public ReadOnly Property Message As String

        Private Sub New(success As Boolean, message As String)
            Me.Success = success
            Me.Message = message
        End Sub

        Public Shared Function Ok(message As String) As ActionResult
            Return New ActionResult(True, message)
        End Function

        Public Shared Function Fail(message As String) As ActionResult
            Return New ActionResult(False, message)
        End Function
    End Class

End Namespace
