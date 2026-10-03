Namespace Voices

    ''' <summary>
    ''' The voice list grouped by locale for the editor's pickers. Built once from a list and never changed
    ''' afterwards (a new list means a new catalog). Lookups ignore case. Entries without a locale or short name
    ''' are skipped and duplicate short names keep the first.
    ''' </summary>
    Public NotInheritable Class VoiceCatalog

        Public Shared ReadOnly Empty As New VoiceCatalog(Array.Empty(Of VoiceInfo)())

        Private ReadOnly _byLocale As New Dictionary(Of String, List(Of VoiceInfo))(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _byShortName As New Dictionary(Of String, VoiceInfo)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _locales As New List(Of String)()
        Private ReadOnly _all As New List(Of VoiceInfo)()

        Public Sub New(voices As IEnumerable(Of VoiceInfo))
            If voices Is Nothing Then Throw New ArgumentNullException(NameOf(voices))

            For Each v In voices
                If v Is Nothing Then Continue For
                If String.IsNullOrWhiteSpace(v.ShortName) OrElse String.IsNullOrWhiteSpace(v.Locale) Then Continue For
                If Not _byShortName.TryAdd(v.ShortName.Trim(), v) Then Continue For

                Dim group As List(Of VoiceInfo) = Nothing
                If Not _byLocale.TryGetValue(v.Locale.Trim(), group) Then
                    group = New List(Of VoiceInfo)()
                    _byLocale.Add(v.Locale.Trim(), group)
                    _locales.Add(v.Locale.Trim())
                End If
                group.Add(v)
                _all.Add(v)
            Next

            _locales.Sort(StringComparer.OrdinalIgnoreCase)
            For Each group In _byLocale.Values
                group.Sort(Function(a, b) String.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase))
            Next
        End Sub

        ''' <summary>Number of voices in the catalog.</summary>
        Public ReadOnly Property Count As Integer
            Get
                Return _all.Count
            End Get
        End Property

        ''' <summary>Every locale that has at least one voice, sorted.</summary>
        Public ReadOnly Property Locales As IReadOnlyList(Of String)
            Get
                Return _locales
            End Get
        End Property

        ''' <summary>The voices for a locale, sorted by name. Empty (never Nothing) if there are none.</summary>
        Public Function VoicesFor(locale As String) As IReadOnlyList(Of VoiceInfo)
            If String.IsNullOrWhiteSpace(locale) Then Return Array.Empty(Of VoiceInfo)()
            Dim group As List(Of VoiceInfo) = Nothing
            If _byLocale.TryGetValue(locale.Trim(), group) Then Return group
            Return Array.Empty(Of VoiceInfo)()
        End Function

        ''' <summary>The voice with this short name, or Nothing.</summary>
        Public Function TryFind(shortName As String) As VoiceInfo
            If String.IsNullOrWhiteSpace(shortName) Then Return Nothing
            Dim found As VoiceInfo = Nothing
            _byShortName.TryGetValue(shortName.Trim(), found)
            Return found
        End Function

        Public Function Contains(shortName As String) As Boolean
            Return TryFind(shortName) IsNot Nothing
        End Function

    End Class

End Namespace
