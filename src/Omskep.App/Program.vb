Imports System.IO
Imports Omskep.App.Secrets
Imports Omskep.Core.Session
Imports Omskep.Core.Speech

Friend Module Program

    <STAThread()>
    Friend Sub Main(args As String())
        ' Must come before any window exists: route unexpected errors to our handlers instead of Windows' crash dialog.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
        AddHandler Application.ThreadException, AddressOf OnThreadException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnDomainException

        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Omskep")

        ' One HttpClient for the whole run (redirects off, so the key can never be forwarded elsewhere).
        Using http = AzureHttpClientFactory.CreateClient()
            Dim session = AppSession.Create(folder, New DpapiSecretProtector(), http)
            Application.Run(New MainForm(session))
        End Using
    End Sub

    ' The messages deliberately show only the kind of error, never its text or anything near the key.
    Private Sub OnThreadException(sender As Object, e As Threading.ThreadExceptionEventArgs)
        MessageBox.Show(
            "Something unexpected went wrong (" & e.Exception.GetType().Name & "). Omskep will keep running, " &
            "but if anything looks wrong, close it and start it again.",
            "Omskep", MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

    Private Sub OnDomainException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = TryCast(e.ExceptionObject, Exception)
        Dim kind = If(ex Is Nothing, "unknown error", ex.GetType().Name)
        MessageBox.Show(
            "Omskep hit an unexpected error (" & kind & ") and has to close. Please start it again.",
            "Omskep", MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

End Module
