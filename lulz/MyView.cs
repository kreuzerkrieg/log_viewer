namespace lulz
{
    using Terminal.Gui;


    public partial class MyView
    {
        private LogDatabase? _db;

        public MyView()
        {
            InitializeComponent();
            ApplyTurboVisionTheme();
        }

        private static void ShowHelp()
        {
            MessageBox.Query(
                60, 14,
                "Help — Log Commander",
                "  Log Commander v0.1\n\n" +
                "  F1   This help screen\n" +
                "  F3   Open a log file (additive — keeps previous nodes)\n" +
                "  F4   New session (clears all loaded nodes)\n" +
                "  F10  Exit\n\n" +
                "  Use arrow keys to navigate the log table.\n" +
                "  Ctrl+Q also exits at any time.",
                "  OK  ");
        }

        private void OpenLog()
        {
            // Defer to the next idle tick — calling Application.Run() directly
            // inside ProcessKey re-enters the event loop synchronously and the
            // dialog never gets a chance to render.
            Application.MainLoop.AddIdle(() =>
            {
                RunOpenDialog();
                return false; // one-shot; do not reschedule
            });
        }

        private void RunOpenDialog()
        {
            var dialog = new OpenDialog("Open Log File", "Select a Scylla/Seastar log file");
            Application.Run(dialog);

            if (dialog.Canceled || dialog.FilePath is null)
                return;

            var path = dialog.FilePath.ToString()!;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            // First file: create a fresh database.
            // Subsequent files: append into the existing one (additive).
            if (_db is null)
            {
                _db = new LogDatabase();
            }
            else
            {
                var node = Path.GetFileNameWithoutExtension(path);
                if (_db.ContainsNode(node))
                {
                    MessageBox.Query(50, 7, "Already loaded",
                        $"'{node}' is already in the current session.", "OK");
                    return;
                }
            }

            LoadLog(path);
        }

        private void NewSession()
        {
            if (_db is null) return;
            _db.Dispose();
            _db = null;
            tableView.Table = BuildEmptyTable();
            tableView.SetNeedsDisplay();
            Title = "Log Commander";
        }

        private void LoadLog(string path)
        {
            int count;
            try
            {
                count = _db!.Insert(LogParser.Parse(path));
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(60, 8, "Error loading log", ex.Message, "OK");
                return;
            }

            if (count == 0)
            {
                MessageBox.Query(60, 7, "No entries",
                    "No parseable log lines were found in the selected file.", "OK");
                return;
            }

            RefreshTable();
        }

        private void RefreshTable()
        {
            var dt = _db!.Query();
            dt.Columns[0].ColumnName = "Node";
            dt.Columns[1].ColumnName = "Timestamp";
            dt.Columns[2].ColumnName = "Level";
            dt.Columns[3].ColumnName = "Shard";
            dt.Columns[4].ColumnName = "Group";
            dt.Columns[5].ColumnName = "Facility";
            dt.Columns[6].ColumnName = "Message";

            tableView.Table = dt;
            tableView.SetNeedsDisplay();

            var nodes = _db.LoadedNodes();
            var nodeList = string.Join(", ", nodes);
            Title = $"Log Commander — [{nodeList}] ({dt.Rows.Count:N0} lines)";
        }

        private static System.Data.DataTable BuildEmptyTable()
        {
            var dt = new System.Data.DataTable();
            foreach (var col in new[] { "Node", "Timestamp", "Level", "Shard", "Group", "Facility", "Message" })
                dt.Columns.Add(col);
            return dt;
        }

        private void ApplyTurboVisionTheme()
        {
            // Window interior: white on dark blue — classic TV window background
            var windowScheme = new ColorScheme
            {
                Normal    = Application.Driver.MakeAttribute(Color.White,        Color.Blue),
                HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Blue),
                Focus     = Application.Driver.MakeAttribute(Color.Black,        Color.Cyan),
                HotFocus  = Application.Driver.MakeAttribute(Color.Black,        Color.BrightCyan),
                Disabled  = Application.Driver.MakeAttribute(Color.Gray,         Color.Blue)
            };

            // Menu bar + status bar: black on bright white — ANSI 15 renders as actual light bar
            // (Color.Gray = ANSI 7 renders near-black on dark-theme terminals, so use White instead)
            var chromeScheme = new ColorScheme
            {
                Normal    = Application.Driver.MakeAttribute(Color.Black,        Color.White),
                HotNormal = Application.Driver.MakeAttribute(Color.Blue,         Color.White),
                Focus     = Application.Driver.MakeAttribute(Color.Black,        Color.Green),
                HotFocus  = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Green),
                Disabled  = Application.Driver.MakeAttribute(Color.DarkGray,     Color.White)
            };

            // Table / list pane: white on blue, cyan highlight row
            var tableScheme = new ColorScheme
            {
                Normal    = Application.Driver.MakeAttribute(Color.White,        Color.Blue),
                HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Blue),
                Focus     = Application.Driver.MakeAttribute(Color.Black,        Color.Cyan),
                HotFocus  = Application.Driver.MakeAttribute(Color.Black,        Color.BrightCyan),
                Disabled  = Application.Driver.MakeAttribute(Color.Gray,         Color.Blue)
            };

            ColorScheme              = windowScheme;
            tableView.ColorScheme    = tableScheme;
            statusBar.ColorScheme    = chromeScheme;
            menuBar.ColorScheme      = chromeScheme;

            Border.BorderStyle     = BorderStyle.Double;
            Border.Effect3D        = false;
            Border.DrawMarginFrame = true;

            tableView.Style.AlwaysShowHeaders               = true;
            tableView.Style.ExpandLastColumn                = true;
            tableView.Style.InvertSelectedCellFirstCharacter = false;
            tableView.Style.ShowHorizontalHeaderOverline    = false;
            tableView.Style.ShowHorizontalHeaderUnderline   = true;
            tableView.Style.ShowVerticalCellLines           = false;
            tableView.Style.ShowVerticalHeaderLines         = false;

            // Re-wire status bar items with proper Borland-style F-key shortcuts and actions
            help    = new StatusItem(Key.F1,  "~F1~ Help",        ShowHelp);
            openLog = new StatusItem(Key.F3,  "~F3~ Open Log",    OpenLog);
            quit    = new StatusItem(Key.F10, "~F10~ Quit",       () => Application.RequestStop());
            var newSession = new StatusItem(Key.F4, "~F4~ New Session", NewSession);
            statusBar.Items = new[] { help, openLog, newSession, quit };
        }

        public override bool ProcessKey(KeyEvent keyEvent)
        {
            switch (keyEvent.Key)
            {
                case Key.F1:
                    ShowHelp();
                    return true;
                case Key.F3:
                    OpenLog();
                    return true;
                case Key.F4:
                    NewSession();
                    return true;
                case Key.F10:
                case Key.CtrlMask | Key.Q:
                    Application.RequestStop();
                    return true;
            }

            return base.ProcessKey(keyEvent);
        }
    }
}
