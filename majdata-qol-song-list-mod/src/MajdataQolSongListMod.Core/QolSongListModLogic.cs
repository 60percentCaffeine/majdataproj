namespace MajdataQolSongListMod.Core
{
    public sealed class QolSongListModLogic
    {
        private readonly MapListSettings _settings;

        public QolSongListModLogic()
            : this(MapListSettings.Defaults())
        {
        }

        public QolSongListModLogic(MapListSettings settings)
        {
            _settings = settings;
        }

        public MapListSettings Settings
        {
            get { return _settings; }
        }

        public string StartupMessage()
        {
            return "Majdata QoL Song List core ready: " + _settings.Describe();
        }
    }
}
