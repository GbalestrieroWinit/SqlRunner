using System.Collections.ObjectModel;
using System.Data;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MySqlConnector;

namespace SqlRunner;

public sealed class MainWindow : Window
{
    private readonly string connectionsFilePath = Path.Combine(AppContext.BaseDirectory, "connections.json");
    private readonly ObservableCollection<DatabaseConnection> connections = [];
    private readonly ObservableCollection<string> databases = [];
    private readonly ObservableCollection<string> tables = [];

    private DatabaseConnection? currentConnection;

    private ComboBox profilesCombo = null!;
    private TextBox profileNameText = null!;
    private TextBox serverText = null!;
    private NumericUpDown portInput = null!;
    private TextBox userText = null!;
    private TextBox passwordText = null!;
    private ComboBox databasesCombo = null!;
    private TextBlock activeConnectionText = null!;
    private TextBox queryText = null!;
    private TextBlock queryFeedbackText = null!;
    private DataGrid queryResultsGrid = null!;
    private ListBox tablesList = null!;
    private TextBlock tableFeedbackText = null!;
    private DataGrid tableDataGrid = null!;

    public MainWindow()
    {
        Title = "SQL Runner";
        Width = 1180;
        Height = 760;
        MinWidth = 980;
        MinHeight = 640;

        Content = BuildUi();
        LoadConnections();
    }

    private Control BuildUi()
    {
        return new TabControl
        {
            Margin = new Thickness(12),
            Items =
            {
                new TabItem { Header = "Connetti", Content = BuildConnectionPage() },
                new TabItem { Header = "Esegui Query", Content = BuildQueryPage() },
                new TabItem { Header = "Tabelle", Content = BuildTablesPage() }
            }
        };
    }

    private Control BuildConnectionPage()
    {
        var root = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 8
        };

        root.Children.Add(SectionTitle("Login MariaDB"));

        profilesCombo = new ComboBox
        {
            ItemsSource = connections,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        profilesCombo.SelectionChanged += (_, _) => PopulateConnectionFields();
        root.Children.Add(profilesCombo);

        root.Children.Add(Label("Nome profilo:"));
        profileNameText = CreateTextBox();
        root.Children.Add(profileNameText);

        root.Children.Add(Label("Server:"));
        serverText = CreateTextBox("localhost");
        root.Children.Add(serverText);

        root.Children.Add(Label("Porta:"));
        portInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 65535,
            Value = 3306,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        root.Children.Add(portInput);

        root.Children.Add(Label("Utente:"));
        userText = CreateTextBox("root");
        root.Children.Add(userText);

        root.Children.Add(Label("Password:"));
        passwordText = CreateTextBox();
        passwordText.PasswordChar = '*';
        root.Children.Add(passwordText);

        var loginButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 8)
        };
        loginButtons.Children.Add(Button("Login", LoginAndLoadDatabases));
        loginButtons.Children.Add(Button("Salva profilo", SaveConnection));
        loginButtons.Children.Add(Button("Nuovo", ClearConnectionFields));
        loginButtons.Children.Add(Button("Cancella profilo", DeleteConnection));
        root.Children.Add(loginButtons);

        root.Children.Add(SectionTitle("Database disponibili"));
        databasesCombo = new ComboBox
        {
            ItemsSource = databases,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        root.Children.Add(databasesCombo);
        root.Children.Add(Button("Usa database selezionato", UseSelectedDatabase));

        activeConnectionText = new TextBlock
        {
            Text = "Nessuna Connessione Attiva",
            Margin = new Thickness(0, 8, 0, 0)
        };
        root.Children.Add(activeConnectionText);

        return new ScrollViewer { Content = root };
    }

    private Control BuildQueryPage()
    {
        var root = new Grid
        {
            Margin = new Thickness(18),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(1.5, GridUnitType.Star)),
                new RowDefinition(new GridLength(96))
            }
        };

        queryText = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.NoWrap,
            Watermark = "Scrivi qui il comando SQL..."
        };
        Grid.SetRow(queryText, 0);
        root.Children.Add(queryText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8)
        };
        buttons.Children.Add(Button("Esegui", RunSql));
        buttons.Children.Add(Button("Pulisci Output", ClearQueryOutput));
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        var outputLabel = SectionTitle("Output:");
        Grid.SetRow(outputLabel, 2);
        root.Children.Add(outputLabel);

        queryResultsGrid = new DataGrid
        {
            AutoGenerateColumns = true,
            IsReadOnly = true,
            GridLinesVisibility = DataGridGridLinesVisibility.All
        };
        Grid.SetRow(queryResultsGrid, 3);
        root.Children.Add(queryResultsGrid);

        queryFeedbackText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(queryFeedbackText, 4);
        root.Children.Add(new ScrollViewer { Content = queryFeedbackText });

        return root;
    }

    private Control BuildTablesPage()
    {
        var split = new Grid
        {
            Margin = new Thickness(18),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(320)),
                new ColumnDefinition(new GridLength(12)),
                new ColumnDefinition(GridLength.Star)
            }
        };

        var left = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        var refreshButton = Button("Ricarica Tabelle", LoadTables);
        Grid.SetRow(refreshButton, 0);
        left.Children.Add(refreshButton);

        tablesList = new ListBox
        {
            ItemsSource = tables,
            Margin = new Thickness(0, 8, 0, 0)
        };
        tablesList.DoubleTapped += (_, _) => LoadSelectedTableData();
        Grid.SetRow(tablesList, 1);
        left.Children.Add(tablesList);

        Grid.SetColumn(left, 0);
        split.Children.Add(left);

        var right = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(96))
            }
        };

        tableDataGrid = new DataGrid
        {
            AutoGenerateColumns = true,
            IsReadOnly = true,
            GridLinesVisibility = DataGridGridLinesVisibility.All
        };
        Grid.SetRow(tableDataGrid, 0);
        right.Children.Add(tableDataGrid);

        tableFeedbackText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(tableFeedbackText, 1);
        right.Children.Add(new ScrollViewer { Content = tableFeedbackText });

        Grid.SetColumn(right, 2);
        split.Children.Add(right);

        return split;
    }

    private void LoadConnections()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(connectionsFilePath)!);

        if (File.Exists(connectionsFilePath))
        {
            var json = File.ReadAllText(connectionsFilePath);
            foreach (var connection in JsonSerializer.Deserialize<List<DatabaseConnection>>(json) ?? [])
            {
                ApplyLegacyConnectionString(connection);
                connections.Add(connection);
            }
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

        profilesCombo.SelectedIndex = 0;
    }

    private void PopulateConnectionFields()
    {
        if (profilesCombo.SelectedItem is not DatabaseConnection connection)
        {
            return;
        }

        ApplyLegacyConnectionString(connection);
        profileNameText.Text = connection.Name;
        serverText.Text = connection.Server;
        portInput.Value = connection.Port == 0 ? 3306 : connection.Port;
        userText.Text = connection.User;
        passwordText.Text = connection.Password;
    }

    private void ClearConnectionFields()
    {
        profilesCombo.SelectedItem = null;
        profileNameText.Text = "";
        serverText.Text = "localhost";
        portInput.Value = 3306;
        userText.Text = "root";
        passwordText.Text = "";
        databases.Clear();
        databasesCombo.IsEnabled = false;
        currentConnection = null;
        activeConnectionText.Text = "Nessuna Connessione Attiva";
    }

    private void SaveConnection()
    {
        var name = profileNameText.Text?.Trim() ?? "";
        var server = serverText.Text?.Trim() ?? "";
        var user = userText.Text?.Trim() ?? "";

        if (name.Length == 0 || server.Length == 0 || user.Length == 0)
        {
            activeConnectionText.Text = "Inserire nome profilo, server e utente.";
            return;
        }

        var savedConnection = new DatabaseConnection
        {
            Name = name,
            Server = server,
            Port = (int)(portInput.Value ?? 3306),
            User = user,
            Password = passwordText.Text ?? "",
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
        profilesCombo.SelectedItem = connections.First(c => c.Name == name);
        activeConnectionText.Text = "Profilo salvato.";
    }

    private void DeleteConnection()
    {
        if (profilesCombo.SelectedItem is not DatabaseConnection selected)
        {
            return;
        }

        connections.Remove(selected);
        PersistConnections();
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
        var serverConnectionString = BuildServerConnectionString();

        try
        {
            using var connection = new MySqlConnection(serverConnectionString);
            connection.Open();
            using var command = new MySqlCommand("SHOW DATABASES", connection);
            using var reader = command.ExecuteReader();

            databases.Clear();
            while (reader.Read())
            {
                databases.Add(reader.GetString(0));
            }

            currentConnection = new DatabaseConnection
            {
                Name = profileNameText.Text?.Trim() ?? "",
                Server = serverText.Text?.Trim() ?? "",
                Port = (int)(portInput.Value ?? 3306),
                User = userText.Text?.Trim() ?? "",
                Password = passwordText.Text ?? "",
                ConnectionString = serverConnectionString
            };

            databasesCombo.IsEnabled = databases.Count > 0;
            activeConnectionText.Text = "Login eseguito. Seleziona un database.";
            queryFeedbackText.Text = $"Login eseguito. Database disponibili: {databases.Count}.";
            tableFeedbackText.Text = $"Login eseguito. Database disponibili: {databases.Count}.";

            if (!string.IsNullOrWhiteSpace(profileNameText.Text))
            {
                SaveConnection();
            }
        }
        catch (Exception ex)
        {
            currentConnection = null;
            databases.Clear();
            databasesCombo.IsEnabled = false;
            activeConnectionText.Text = $"Login fallito: {ex.Message}";
        }
    }

    private void UseSelectedDatabase()
    {
        if (databasesCombo.SelectedItem is not string databaseName)
        {
            activeConnectionText.Text = "Seleziona un database.";
            return;
        }

        currentConnection = new DatabaseConnection
        {
            Name = string.IsNullOrWhiteSpace(profileNameText.Text) ? databaseName : profileNameText.Text.Trim(),
            Server = serverText.Text?.Trim() ?? "",
            Port = (int)(portInput.Value ?? 3306),
            User = userText.Text?.Trim() ?? "",
            Password = passwordText.Text ?? "",
            Database = databaseName,
            ConnectionString = BuildDatabaseConnectionString(databaseName)
        };

        activeConnectionText.Text = $"Database attivo: {databaseName}";
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

        var sql = queryText.Text?.Trim() ?? "";
        if (sql.Length == 0)
        {
            queryFeedbackText.Text = "Inserire un comando SQL da eseguire.";
            return;
        }

        ClearQueryOutput();

        var result = DatabaseHelper.ExecuteQuery(sql, connectionString);
        queryResultsGrid.ItemsSource = result["tabella"] is DataTable table ? table.DefaultView : null;
        queryFeedbackText.Text = Convert.ToString(result["feedback"]);
    }

    private void ClearQueryOutput()
    {
        queryResultsGrid.ItemsSource = null;
        queryFeedbackText.Text = "";
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
              AND TABLE_SCHEMA = DATABASE()
            ORDER BY TABLE_NAME
            """;

        var content = DatabaseHelper.ExecuteQuery(sql, connectionString);
        tables.Clear();

        if (content["tabella"] is DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                tables.Add(Convert.ToString(row["TABLE_NAME"]) ?? "");
            }

            tableFeedbackText.Text = $"Tabelle caricate. Numero: {tables.Count}.";
        }
        else
        {
            tableFeedbackText.Text = Convert.ToString(content["feedback"]);
        }
    }

    private void LoadSelectedTableData()
    {
        if (!TryGetConnection(out var connectionString) || tablesList.SelectedItem is not string tableName)
        {
            return;
        }

        var sql = $"SELECT * FROM {QuoteMariaDbName(tableName)}";
        var content = DatabaseHelper.ExecuteQuery(sql, connectionString);

        if (content["tabella"] is DataTable table)
        {
            tableDataGrid.ItemsSource = table.DefaultView;
            tableFeedbackText.Text = $"Tabella caricata: {tableName}. Righe: {table.Rows.Count}.";
        }
        else
        {
            tableDataGrid.ItemsSource = null;
            tableFeedbackText.Text = Convert.ToString(content["feedback"]);
        }
    }

    private bool TryGetConnection(out string connectionString)
    {
        connectionString = currentConnection?.ConnectionString ?? "";

        if (connectionString.Length > 0)
        {
            return true;
        }

        queryFeedbackText.Text = "Effettuare il login e selezionare un database.";
        tableFeedbackText.Text = "Effettuare il login e selezionare un database.";
        return false;
    }

    private string BuildServerConnectionString()
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = serverText.Text?.Trim() ?? "",
            Port = (uint)(portInput.Value ?? 3306),
            UserID = userText.Text?.Trim() ?? "",
            Password = passwordText.Text ?? ""
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

    private static TextBox CreateTextBox(string text = "")
    {
        return new TextBox
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static TextBlock Label(string text)
    {
        return new TextBlock { Text = text };
    }

    private static TextBlock SectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 8, 0, 2)
        };
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static string QuoteMariaDbName(string value)
    {
        return $"`{value.Replace("`", "``")}`";
    }

    private sealed class DatabaseConnection
    {
        public string Name { get; set; } = "";
        public string Server { get; set; } = "";
        public int Port { get; set; } = 3306;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string Database { get; set; } = "";
        public string ConnectionString { get; set; } = "";

        public override string ToString()
        {
            return Name;
        }
    }
}
