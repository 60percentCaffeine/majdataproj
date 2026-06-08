namespace MajdataQolSongListMod.Core
{
    public sealed class QolSongListModLogic
    {
        private readonly MapListSettings _settings;
        private readonly ActivePlayerSession _activePlayerSession;

        public QolSongListModLogic()
            : this(MapListSettings.Defaults(), new ActivePlayerSession())
        {
        }

        public QolSongListModLogic(MapListSettings settings)
            : this(settings, new ActivePlayerSession())
        {
        }

        public QolSongListModLogic(MapListSettings settings, ActivePlayerSession activePlayerSession)
        {
            _settings = settings;
            _activePlayerSession = activePlayerSession ?? new ActivePlayerSession();
        }

        public MapListSettings Settings
        {
            get { return _settings; }
        }

        public ActivePlayerSession ActivePlayerSession
        {
            get { return _activePlayerSession; }
        }

        public string StartupMessage()
        {
            return "Majdata QoL Song List core ready: " + _settings.Describe();
        }

        public string StartupDiagnostics()
        {
            return "Majdata QoL Song List diagnostics: " + _activePlayerSession.ToDiagnosticString();
        }
    }
}
