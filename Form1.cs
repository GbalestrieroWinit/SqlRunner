using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace SqlRunner
{
    public partial class Form1 : Form
    {
        private readonly BindingSource connectionsSource = new();
        private readonly BindingSource tablesSource = new();
        private readonly string connectionsFilePath;

        private List<DatabaseConnection> connections = [];
        private DatabaseConnection? currentConnection;

        private ComboBox connectionsCombo = null!;
        private TextBox connectionNameText = null!;
        private TextBox serverText = null!;
        private NumericUpDown portInput = null!;
        private TextBox userText = null!;
        private TextBox passwordText = null!;
        private ComboBox databasesCombo = null!;
        private RichTextBox queryText = null!;
        private TextBox queryFeedbackText = null!;
        private DataGridView queryResultsGrid = null!;
        private DataGridView tablesGrid = null!;
        private DataGridView tableDataGrid = null!;
        private TextBox tableFeedbackText = null!;
        private Label activeConnectionLabel = null!;
        private bool isHighlightingSql;

        private static readonly Regex SqlIdentifierRegex = new(@"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
        private static readonly Regex SqlKeywordRegex = new(@"\b(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TRUNCATE|RENAME|REPLACE|WITH|FROM|WHERE|JOIN|INNER|LEFT|RIGHT|FULL|OUTER|CROSS|ON|GROUP|BY|ORDER|HAVING|LIMIT|OFFSET|VALUES|SET|INTO|AS|AND|OR|NOT|NULL|IS|IN|BETWEEN|LIKE|EXISTS|CASE|WHEN|THEN|ELSE|END|DISTINCT|UNION|ALL|PRIMARY|KEY|FOREIGN|REFERENCES|INDEX|TABLE|DATABASE|VIEW|PROCEDURE|FUNCTION|TRIGGER|BEGIN|COMMIT|ROLLBACK|DESC|DESCRIBE|SHOW|USE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SqlStringRegex = new(@"'([^'\\]|\\.|'')*'|""([^""\\]|\\.|"""")*""", RegexOptions.Compiled);
        private static readonly Regex SqlNumberRegex = new(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
        private static readonly Regex SqlCommentRegex = new(@"(--[^\r\n]*|/\*.*?\*/)", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex SqlQuotedIdentifierRegex = new(@"`[^`]*(?:``[^`]*)*`", RegexOptions.Compiled);

        public Form1()
        {
            InitializeComponent();
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
            connectionsFilePath = Path.Combine(AppContext.BaseDirectory, "connections.json");

            BuildUi();
            LoadConnections();
        }

        private void BuildUi()
        {
            Text = "SQL Runner";
            MinimumSize = new Size(1100, 720);
            ClientSize = new Size(1180, 760);
            StartPosition = FormStartPosition.CenterScreen;

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 6)
            };

            tabs.TabPages.Add(BuildConnectionPage());
            tabs.TabPages.Add(BuildQueryPage());
            tabs.TabPages.Add(BuildTablesPage());

            Controls.Add(tabs);
        }

        private TabPage BuildConnectionPage()
        {
            var page = new TabPage("Connetti");

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 14
            };
            for (var i = 0; i < 14; i++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            root.Controls.Add(new Label
            {
                Text = "Login MariaDB",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);

            connectionsCombo = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DisplayMember = nameof(DatabaseConnection.Name),
                DataSource = connectionsSource,
                Margin = new Padding(0, 0, 0, 12)
            };
            connectionsCombo.SelectedIndexChanged += (_, _) => PopulateConnectionFields();
            root.Controls.Add(connectionsCombo, 0, 1);

            root.Controls.Add(new Label { Text = "Nome profilo:", AutoSize = true }, 0, 2);
            connectionNameText = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 12) };
            root.Controls.Add(connectionNameText, 0, 3);

            root.Controls.Add(new Label { Text = "Server:", AutoSize = true }, 0, 4);
            serverText = new TextBox
            {
                Dock = DockStyle.Top,
                Text = "localhost",
                Margin = new Padding(0, 4, 0, 12)
            };
            root.Controls.Add(serverText, 0, 5);

            root.Controls.Add(new Label { Text = "Porta:", AutoSize = true }, 0, 6);
            portInput = new NumericUpDown
            {
                Dock = DockStyle.Top,
                Minimum = 1,
                Maximum = 65535,
                Value = 3306,
                Margin = new Padding(0, 4, 0, 12)
            };
            root.Controls.Add(portInput, 0, 7);

            root.Controls.Add(new Label { Text = "Utente:", AutoSize = true }, 0, 8);
            userText = new TextBox
            {
                Dock = DockStyle.Top,
                Text = "root",
                Margin = new Padding(0, 4, 0, 12)
            };
            root.Controls.Add(userText, 0, 9);

            root.Controls.Add(new Label { Text = "Password:", AutoSize = true }, 0, 10);
            passwordText = new TextBox
            {
                Dock = DockStyle.Top,
                UseSystemPasswordChar = true,
                Margin = new Padding(0, 4, 0, 12)
            };
            root.Controls.Add(passwordText, 0, 11);

            var loginButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0)
            };
            loginButtons.Controls.Add(MakeButton("Login", (_, _) => LoginAndLoadDatabases()));
            loginButtons.Controls.Add(MakeButton("Salva profilo", (_, _) => SaveConnection()));
            loginButtons.Controls.Add(MakeButton("Nuovo", (_, _) => ClearConnectionFields()));
            loginButtons.Controls.Add(MakeButton("Cancella profilo", (_, _) => DeleteConnection()));
            root.Controls.Add(loginButtons, 0, 12);

            var databasePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0, 10, 0, 0)
            };
            databasePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            databasePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            databasePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            databasePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            databasePanel.Controls.Add(new Label
            {
                Text = "Database disponibili:",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            databasesCombo = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            databasePanel.Controls.Add(databasesCombo, 0, 1);

            databasePanel.Controls.Add(MakeButton("Usa database selezionato", (_, _) => UseSelectedDatabase()), 0, 2);
            root.Controls.Add(databasePanel, 0, 13);

            activeConnectionLabel = new Label
            {
                Text = "Nessuna Connessione Attiva",
                AutoSize = true,
                Padding = new Padding(0, 14, 0, 0)
            };
            databasePanel.Controls.Add(activeConnectionLabel, 0, 3);

            page.Controls.Add(root);
            return page;
        }

        private TabPage BuildQueryPage()
        {
            var page = new TabPage("Esegui Query");

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));

            queryText = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsTab = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 10),
                Text = ""
            };
            queryText.TextChanged += (_, _) => HighlightSql();
            root.Controls.Add(queryText, 0, 0);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
            buttons.Controls.Add(MakeButton("Esegui", (_, _) => RunSql()));
            buttons.Controls.Add(MakeButton("Pulisci Output", (_, _) => ClearQueryOutput()));
            root.Controls.Add(buttons, 0, 1);

            root.Controls.Add(new Label
            {
                Text = "Output:",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 6)
            }, 0, 2);

            queryResultsGrid = MakeGrid();
            root.Controls.Add(queryResultsGrid, 0, 3);

            queryFeedbackText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                BackColor = SystemColors.Window
            };
            root.Controls.Add(queryFeedbackText, 0, 4);

            page.Controls.Add(root);
            return page;
        }

        private TabPage BuildTablesPage()
        {
            var page = new TabPage("Tabelle");

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 360,
                Padding = new Padding(18)
            };

            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            left.Controls.Add(MakeButton("Ricarica Tabelle", (_, _) => LoadTables()), 0, 0);

            tablesGrid = MakeGrid();
            tablesGrid.DataSource = tablesSource;
            tablesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            tablesGrid.CellDoubleClick += (_, _) => LoadSelectedTableData();
            left.Controls.Add(tablesGrid, 0, 1);

            left.Controls.Add(MakeButton("Apri Tabella selezionata", (_, _) => LoadSelectedTableData()), 0, 2);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));

            tableDataGrid = MakeGrid();
            right.Controls.Add(tableDataGrid, 0, 0);

            tableFeedbackText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                BackColor = SystemColors.Window
            };
            right.Controls.Add(tableFeedbackText, 0, 1);

            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(right);
            page.Controls.Add(split);
            return page;
        }

        private static Button MakeButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 8, 8)
            };
            button.Click += onClick;
            return button;
        }

        private static DataGridView MakeGrid() => new()
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };

        private void LoadConnections()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(connectionsFilePath)!);

            if (File.Exists(connectionsFilePath))
            {
                var json = File.ReadAllText(connectionsFilePath);
                connections = JsonSerializer.Deserialize<List<DatabaseConnection>>(json) ?? [];
            }

            if (connections.Count == 0)
            {
                connections.Add(new DatabaseConnection
                {
                    Name = "Local MariaDB",
                    Server = "localhost",
                    Port = 3306,
                    User = "root",
                    Password = "root"
                });
            }

            RefreshConnectionsSource();
        }

        private void RefreshConnectionsSource()
        {
            connectionsSource.DataSource = null;
            connectionsSource.DataSource = connections;
            PopulateConnectionFields();
        }

        private void PopulateConnectionFields()
        {
            if (connectionsCombo.SelectedItem is not DatabaseConnection connection)
            {
                return;
            }

            connectionNameText.Text = connection.Name;
            ApplyLegacyConnectionString(connection);
            serverText.Text = connection.Server;
            portInput.Value = Math.Clamp(connection.Port, (int)portInput.Minimum, (int)portInput.Maximum);
            userText.Text = connection.User;
            passwordText.Text = connection.Password;
        }

        private void ClearConnectionFields()
        {
            connectionsCombo.SelectedIndex = -1;
            connectionNameText.Clear();
            serverText.Text = "localhost";
            portInput.Value = 3306;
            userText.Text = "root";
            passwordText.Clear();
            databasesCombo.DataSource = null;
            databasesCombo.Enabled = false;
            activeConnectionLabel.Text = "Nessuna Connessione Attiva";
        }

        private void SaveConnection()
        {
            var name = connectionNameText.Text.Trim();
            var server = serverText.Text.Trim();
            var user = userText.Text.Trim();

            if (name.Length == 0 || server.Length == 0 || user.Length == 0)
            {
                MessageBox.Show("Inserire nome profilo, server e utente.", "Dettagli Mancanti");
                return;
            }

            var savedConnection = new DatabaseConnection
            {
                Name = name,
                Server = server,
                Port = (int)portInput.Value,
                User = user,
                Password = passwordText.Text,
                ConnectionString = BuildServerConnectionString()
            };

            var existing = connections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                connections.Add(savedConnection);
            }
            else
            {
                existing.Server = savedConnection.Server;
                existing.Port = savedConnection.Port;
                existing.User = savedConnection.User;
                existing.Password = savedConnection.Password;
                existing.ConnectionString = savedConnection.ConnectionString;
            }

            PersistConnections();
            RefreshConnectionsSource();
            connectionsCombo.SelectedItem = connections.First(c => c.Name == name);
        }

        private void DeleteConnection()
        {
            if (connectionsCombo.SelectedItem is not DatabaseConnection selected)
            {
                return;
            }

            connections.Remove(selected);
            PersistConnections();
            RefreshConnectionsSource();
            ClearConnectionFields();
        }

        private void PersistConnections()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(connectionsFilePath)!);
            var json = JsonSerializer.Serialize(connections, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(connectionsFilePath, json);
        }

        private void LoginAndLoadDatabases()
        {
            var connection = new DatabaseConnection
            {
                Name = connectionNameText.Text.Trim(),
                Server = serverText.Text.Trim(),
                Port = (int)portInput.Value,
                User = userText.Text.Trim(),
                Password = passwordText.Text,
                ConnectionString = BuildServerConnectionString()
            };

            if (connection.Server.Length == 0 || connection.User.Length == 0)
            {
                MessageBox.Show("Inserire server e utente.", "Dettagli Mancanti");
                return;
            }

            try
            {
                using var mysqlConnection = new MySqlConnection(connection.ConnectionString);
                mysqlConnection.Open();
                using var command = new MySqlCommand("SHOW DATABASES", mysqlConnection);
                using var reader = command.ExecuteReader();

                var databases = new List<string>();
                while (reader.Read())
                {
                    databases.Add(reader.GetString(0));
                }

                currentConnection = connection;
                databasesCombo.DataSource = databases;
                databasesCombo.Enabled = databases.Count > 0;

                activeConnectionLabel.Text = "Login eseguito. Seleziona un database.";
                queryFeedbackText.Text = $"Login eseguito. Database disponibili: {databases.Count}.";
                tableFeedbackText.Text = $"Login eseguito. Database disponibili: {databases.Count}.";

                if (connection.Name.Length > 0)
                {
                    SaveConnection();
                }
            }
            catch (Exception ex)
            {
                currentConnection = null;
                databasesCombo.DataSource = null;
                databasesCombo.Enabled = false;
                MessageBox.Show(ex.Message, "Login fallito", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UseSelectedDatabase()
        {
            if (databasesCombo.SelectedItem is not string databaseName)
            {
                MessageBox.Show("Seleziona un database.", "Database mancante");
                return;
            }

            currentConnection = new DatabaseConnection
            {
                Name = string.IsNullOrWhiteSpace(connectionNameText.Text) ? databaseName : connectionNameText.Text.Trim(),
                Server = serverText.Text.Trim(),
                Port = (int)portInput.Value,
                User = userText.Text.Trim(),
                Password = passwordText.Text,
                Database = databaseName,
                ConnectionString = BuildDatabaseConnectionString(databaseName)
            };

            activeConnectionLabel.Text = $"Database attivo: {databaseName}";
            queryFeedbackText.Text = $"Database attivo: {databaseName}.";
            tableFeedbackText.Text = $"Database attivo: {databaseName}.";
            LoadTables();
        }

        private void RunSql()
        {
            if (!TryGetConnection(out var connectionString))
            {
                return;
            }

            var sql = queryText.Text.Trim();
            if (sql.Length == 0)
            {
                queryFeedbackText.Text = "Inserire un comando SQL da eseguire.";
                return;
            }

            ClearQueryOutput();

            var result = DatabaseHelper.ExecuteQuery(sql, connectionString);
            queryResultsGrid.DataSource = result["tabella"];
            queryFeedbackText.Text = Convert.ToString(result["feedback"]);
        }

        private void ClearQueryOutput()
        {
            queryResultsGrid.DataSource = null;
            queryFeedbackText.Clear();
        }

        private void HighlightSql()
        {
            if (isHighlightingSql)
            {
                return;
            }

            isHighlightingSql = true;

            var selectionStart = queryText.SelectionStart;
            var selectionLength = queryText.SelectionLength;

            queryText.SuspendLayout();

            queryText.SelectAll();
            queryText.SelectionColor = Color.Black;
            queryText.SelectionFont = new Font(queryText.Font, FontStyle.Regular);

            ApplySqlHighlight(SqlIdentifierRegex, Color.SeaGreen, FontStyle.Regular);
            ApplySqlHighlight(SqlQuotedIdentifierRegex, Color.SeaGreen, FontStyle.Regular);
            ApplySqlHighlight(SqlNumberRegex, Color.DarkViolet, FontStyle.Regular);
            ApplySqlHighlight(SqlKeywordRegex, Color.RoyalBlue, FontStyle.Bold);
            ApplySqlHighlight(SqlStringRegex, Color.Firebrick, FontStyle.Regular);
            ApplySqlHighlight(SqlCommentRegex, Color.Gray, FontStyle.Italic);

            queryText.Select(Math.Min(selectionStart, queryText.TextLength), Math.Min(selectionLength, queryText.TextLength - Math.Min(selectionStart, queryText.TextLength)));
            queryText.SelectionColor = Color.Black;
            queryText.SelectionFont = new Font(queryText.Font, FontStyle.Regular);

            queryText.ResumeLayout();
            isHighlightingSql = false;
        }

        private void ApplySqlHighlight(Regex regex, Color color, FontStyle style)
        {
            foreach (Match match in regex.Matches(queryText.Text))
            {
                if (match.Length == 0)
                {
                    continue;
                }

                queryText.Select(match.Index, match.Length);
                queryText.SelectionColor = color;
                queryText.SelectionFont = new Font(queryText.Font, style);
            }
        }

        private void LoadTables()
        {
            if (!TryGetConnection(out var connectionString))
            {
                return;
            }

            const string sql = """
                SELECT TABLE_NAME
                FROM information_schema.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                  AND (DATABASE() IS NULL OR TABLE_SCHEMA = DATABASE())
                  AND TABLE_SCHEMA NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
                ORDER BY TABLE_NAME
                """;

            var content = DatabaseHelper.ExecuteQuery(sql, connectionString);
            if (content["tabella"] is DataTable table)
            {
                tablesSource.DataSource = table;
                tableFeedbackText.Text = $"Tabelle caricate. Numero: {table.Rows.Count}.";
            }
            else tableFeedbackText.Text = Convert.ToString(content["feedback"]);
            
        }

        private void LoadSelectedTableData()
        {
            if (!TryGetConnection(out var connectionString) || tablesGrid.CurrentRow?.DataBoundItem is not DataRowView row)
            {
                return;
            }

            var tableName = Convert.ToString(row["TABLE_NAME"]) ?? "";
            var sql = $"SELECT * FROM {QuoteMariaDbName(tableName)}";

            var content = DatabaseHelper.ExecuteQuery(sql, connectionString);
            if (content["tabella"] is DataTable table)
            {
                tableDataGrid.DataSource = table;
            }
            else tableFeedbackText.Text = Convert.ToString(content["feedback"]);
        }

        private bool TryGetConnection(out string connectionString)
        {
            connectionString = currentConnection?.ConnectionString ?? "";

            if (connectionString.Length > 0)
            {
                return true;
            }

            MessageBox.Show("Effettuare il login e selezionare un database.", "nessuna connessione attiva");
            return false;
        }

        private string BuildServerConnectionString()
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = serverText.Text.Trim(),
                Port = (uint)portInput.Value,
                UserID = userText.Text.Trim(),
                Password = passwordText.Text
            };

            return builder.ConnectionString;
        }

        private string BuildDatabaseConnectionString(string databaseName)
        {
            var builder = new MySqlConnectionStringBuilder(BuildServerConnectionString())
            {
                Database = databaseName
            };

            return builder.ConnectionString;
        }

        private static void ApplyLegacyConnectionString(DatabaseConnection connection)
        {
            if (string.IsNullOrWhiteSpace(connection.ConnectionString))
            {
                return;
            }

            try
            {
                var builder = new MySqlConnectionStringBuilder(connection.ConnectionString);
                connection.Server = string.IsNullOrWhiteSpace(connection.Server) ? builder.Server : connection.Server;
                connection.Port = connection.Port == 0 ? (int)builder.Port : connection.Port;
                connection.User = string.IsNullOrWhiteSpace(connection.User) ? builder.UserID : connection.User;
                connection.Password = string.IsNullOrEmpty(connection.Password) ? builder.Password : connection.Password;
                connection.Database = string.IsNullOrWhiteSpace(connection.Database) ? builder.Database : connection.Database;
            }
            catch
            {
                // Ignore old malformed profile entries; the user can edit the visible fields.
            }
        }

        private static string QuoteMariaDbName(string value) => $"`{value.Replace("`", "``")}`";



        private sealed class DatabaseConnection
        {
            public string Name { get; set; } = "";
            public string Server { get; set; } = "";
            public int Port { get; set; } = 3306;
            public string User { get; set; } = "";
            public string Password { get; set; } = "";
            public string Database { get; set; } = "";
            public string ConnectionString { get; set; } = "";
        }
    }
}
