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
        private TextBox connectionStringText = null!;
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
            Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "app.ico"));
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
                RowCount = 9
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                Text = "Connessioni Database Salvate",
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

            root.Controls.Add(new Label { Text = "Connessione:", AutoSize = true }, 0, 2);
            connectionNameText = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 12) };
            root.Controls.Add(connectionNameText, 0, 3);

            root.Controls.Add(new Label { Text = "String di connessione MariaDB", AutoSize = true }, 0, 4);
            connectionStringText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10),
                Margin = new Padding(0, 4, 0, 12)
            };
            root.Controls.Add(connectionStringText, 0, 5);

            root.Controls.Add(new Label
            {
                Text = "Esempio: Server=localhost;Port=3306;User=my_user;Password=my_password;  (Database=... e opzionale)",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 12)
            }, 0, 6);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0)
            };

            buttons.Controls.Add(MakeButton("Nuova", (_, _) => ClearConnectionFields()));
            buttons.Controls.Add(MakeButton("Salva", (_, _) => SaveConnection()));
            buttons.Controls.Add(MakeButton("Cancella", (_, _) => DeleteConnection()));
            buttons.Controls.Add(MakeButton("Connetti", (_, _) => TestAndUseConnection()));

            root.Controls.Add(buttons, 0, 7);

            activeConnectionLabel = new Label
            {
                Text = "Nessuna COnnessione Attiva",
                AutoSize = true,
                Padding = new Padding(0, 14, 0, 0)
            };
            root.Controls.Add(activeConnectionLabel, 0, 8);

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
                    ConnectionString = "Server=localhost;Port=3306;User=root;Password=root;"
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
            connectionStringText.Text = connection.ConnectionString;
        }

        private void ClearConnectionFields()
        {
            connectionsCombo.SelectedIndex = -1;
            connectionNameText.Clear();
            connectionStringText.Clear();
        }

        private void SaveConnection()
        {
            var name = connectionNameText.Text.Trim();
            var connectionString = connectionStringText.Text.Trim();

            if (name.Length == 0 || connectionString.Length == 0)
            {
                MessageBox.Show("Inserire sia il nome della connessione che la stringa di connessione.", "Dettagli Mancanti");
                return;
            }

            var existing = connections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                connections.Add(new DatabaseConnection { Name = name, ConnectionString = connectionString });
            }
            else
            {
                existing.ConnectionString = connectionString;
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

        private void TestAndUseConnection()
        {
            var connection = new DatabaseConnection
            {
                Name = connectionNameText.Text.Trim(),
                ConnectionString = connectionStringText.Text.Trim()
            };

            if (connection.Name.Length == 0 || connection.ConnectionString.Length == 0)
            {
                MessageBox.Show("Inserire sia il nome della connessione che la stringa di connessione.", "Dettagli Mancanti");
                return;
            }

            try
            {
                using var mysqlConnection = new MySqlConnection(connection.ConnectionString);
                mysqlConnection.Open();
                using var command = new MySqlCommand("SELECT 1", mysqlConnection);
                command.ExecuteScalar();

                currentConnection = connection;
                activeConnectionLabel.Text = $"Connessione Attiva: {connection.Name}";
                queryFeedbackText.Text = $"Connesso a {connection.Name}.";
                tableFeedbackText.Text = $"Connesso a {connection.Name}.";
                SaveConnection();
                LoadTables();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connessione fallita", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

            try
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                using var command = new MySqlCommand(sql, connection);
                using var reader = command.ExecuteReader();

                if (reader.FieldCount > 0)
                {
                    var table = new DataTable();
                    table.Load(reader);
                    queryResultsGrid.DataSource = table;
                    queryFeedbackText.Text = $"Query eseguita. Righe influenzate: {table.Rows.Count}.";
                }
                else
                {
                    queryResultsGrid.DataSource = null;
                    queryFeedbackText.Text = $"Comando eseguito. Righe influenzate: {reader.RecordsAffected}.";
                }
            }
            catch (MySqlException ex)
            {
                queryFeedbackText.Text = FormatMySqlError(ex);
            }
            catch (Exception ex)
            {
                queryFeedbackText.Text = ex.Message;
            }
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
                SELECT TABLE_SCHEMA, TABLE_NAME
                FROM information_schema.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                  AND (DATABASE() IS NULL OR TABLE_SCHEMA = DATABASE())
                  AND TABLE_SCHEMA NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
                ORDER BY TABLE_SCHEMA, TABLE_NAME
                """;

            try
            {
                var table = ExecuteDataTable(connectionString, sql);
                tablesSource.DataSource = table;
                tableFeedbackText.Text = $"Tabelle caricate. Numero: {table.Rows.Count}.";
            }
            catch (Exception ex)
            {
                tableFeedbackText.Text = ex is MySqlException mysqlException ? FormatMySqlError(mysqlException) : ex.Message;
            }
        }

        private void LoadSelectedTableData()
        {
            if (!TryGetConnection(out var connectionString) || tablesGrid.CurrentRow?.DataBoundItem is not DataRowView row)
            {
                return;
            }

            var schema = Convert.ToString(row["TABLE_SCHEMA"]) ?? "";
            var tableName = Convert.ToString(row["TABLE_NAME"]) ?? "";
            var sql = $"SELECT * FROM {QuoteMariaDbName(schema)}.{QuoteMariaDbName(tableName)}";

            try
            {
                var table = ExecuteDataTable(connectionString, sql);
                tableDataGrid.DataSource = table;
                //tableFeedbackText.Text = $"Eseguito: {sql}{Environment.NewLine}Rows returned: {table.Rows.Count}.";
            }
            catch (Exception ex)
            {
                tableFeedbackText.Text = ex is MySqlException mysqlException ? FormatMySqlError(mysqlException) : ex.Message;
            }
        }

        private bool TryGetConnection(out string connectionString)
        {
            connectionString = currentConnection?.ConnectionString ?? connectionStringText.Text.Trim();

            if (connectionString.Length > 0)
            {
                return true;
            }

            MessageBox.Show("Collegarsi ad un database.", "nessuna connessione attiva");
            return false;
        }

        private static DataTable ExecuteDataTable(string connectionString, string sql)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var command = new MySqlCommand(sql, connection);
            using var reader = command.ExecuteReader();
            var table = new DataTable();
            table.Load(reader);
            return table;
        }

        private static string QuoteMariaDbName(string value) => $"`{value.Replace("`", "``")}`";

        private static string FormatMySqlError(MySqlException ex)
        {
            var lines = new List<string> { "Errore SQL:" };
            lines.Add($"- {ex.Message}");
            lines.Add($"  Codice errore: {ex.ErrorCode}; numero: {ex.Number}; stato SQL: {ex.SqlState}");

            return string.Join(Environment.NewLine, lines);
        }

        private sealed class DatabaseConnection
        {
            public string Name { get; set; } = "";
            public string ConnectionString { get; set; } = "";
        }
    }
}
