using System.Data;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.CSharp.RuntimeBinder;
using Telescope;
using Terminal.Gui;

namespace Telescope_GUI;

public class MainView : Window
{
	private MenuBar _menuBar;
	private QueryInputView _queryInputView;

	private bool _loading;

	private DataTable _dt;
	private TableView _resultsTable;
	private ContextMenu _tableContextMenu;
	private bool _updating;

	private CosmosApiWrapper _cosmosApiWrapper;
	private NavigatorBar _navigatorBar;
	private AppSettings _appSettings;

	private Dialog? _credentialConfigurationDialog;
	private Dialog? _databaseSelectionDialog;
	private Dialog? _containerSelectionDialog;
	private Dialog? _columnConfigurationDialog;

	private List<string> _columns;

	public MainView()
	{
		initializeComponent();
	}

	private void initializeComponent()
	{
		try
		{
			_appSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText("appsettings.json"));
		}
		catch
		{
			_appSettings = new AppSettings();
		}

		_cosmosApiWrapper = new CosmosApiWrapper();
		_cosmosApiWrapper.PageSize = _appSettings.Preferences.PageSize;

		_columns = _appSettings.Columns;

		try
		{
			if (_appSettings.AccountEndpoint is not null && _appSettings.AccountKey is not null)
			{
				_cosmosApiWrapper.SetCredentials(_appSettings.AccountEndpoint, _appSettings.AccountKey);
			}

			if (_appSettings.SelectedDatabase is not null)
			{
				_cosmosApiWrapper.SelectDatabase(_appSettings.SelectedDatabase);
			}

			if (_appSettings.SelectedContainer is not null)
			{
				_cosmosApiWrapper.SelectContainer(_appSettings.SelectedContainer);
			}
		}
		catch
		{
			// ignored
		}

		updateTitle();

		Width = Dim.Fill();
		Height = Dim.Fill();

		_menuBar = new MenuBar
		{
			Menus =
			[
				new MenuBarItem("_File",
				[
					new MenuItem("_Configure CosmosDB credentials", "",
								 async () => await openCredentialConfiguration()),
					new MenuItem("_Select Database", "", async () => await openDatabaseSelection()),
					new MenuItem("_Select Container", "", async () => await openContainerSelection()),
					new MenuItem("_Quit", "", () => Application.RequestStop())
				]),

				new MenuBarItem("_Preferences", Array.Empty<MenuItem>())
			]
		};

		_queryInputView = new QueryInputView(executeQuery)
		{
			X = 0,
			Y = Pos.Bottom(_menuBar) + 1,
			Width = Dim.Fill(),
			Height = 1
		};

		_dt = new DataTable();

		_resultsTable = new TableView
		{
			X = 0,
			Y = Pos.Bottom(_queryInputView) + 1,
			Width = Dim.Fill(),
			Height = Dim.Fill(1),
			Table = _dt,
			Style = new TableView.TableStyle
			{
				AlwaysShowHeaders = true
			}
		};

		_tableContextMenu = new ContextMenu
		{
			MenuItems = new MenuBarItem("",
			[
				new MenuItem("Edit columns", "", async () => await openColumnConfiguration()),
			])
		};

		_resultsTable.MouseClick += e =>
		{
			if (!e.MouseEvent.Flags.HasFlag(MouseFlags.Button3Clicked))
			{
				return;
			}

			_tableContextMenu.Position = new Point(e.MouseEvent.X, e.MouseEvent.Y);
			_tableContextMenu.Show();
		};

		_navigatorBar = new NavigatorBar
		{
			X = 0,
			Y = Pos.Bottom(_resultsTable),
			Width = Dim.Fill(),
			Height = 1
		};
		_navigatorBar.PageChanged += async (page) =>
		{
			if (_updating)
			{
				return;
			}

			tableLoading();

			if (page < _cosmosApiWrapper.Pages.Count)
			{
				updateTable();
				return;
			}

			_navigatorBar.Pages += await _cosmosApiWrapper.LoadMore() ? 1 : 0;
			updateTable();
		};

		updateQueryField(_appSettings.LastQuery);
		updateTable();
		Add(_menuBar, _queryInputView, _resultsTable, _navigatorBar);
	}

	private async Task executeQuery()
	{
		if (_loading)
		{
			return;
		}

		_loading = true;
		_navigatorBar.SetPage(0);
		_navigatorBar.Pages = 0;

		_appSettings.LastQuery = _queryInputView.QueryField.Text.ToString();
		await File.WriteAllTextAsync("appsettings.json", JsonSerializer.Serialize(_appSettings));

		tableLoading();

		try
		{
			bool morePages =
				await _cosmosApiWrapper.GetFirstPageByQueryAsync(_queryInputView.QueryField.Text.ToString());
			_navigatorBar.Pages = _cosmosApiWrapper.Pages.Count + (morePages ? 1 : 0);
		}
		catch (CosmosException e)
		{
			_dt.Rows.Clear();
			MessageBox.ErrorQuery("Error", e.Message, "Ok");
		}
		catch (ArgumentException e)
		{
			_dt.Rows.Clear();
			MessageBox.ErrorQuery("Error", e.Message, "Ok");
		}
		finally
		{
			updateTable();
			_loading = false;
		}
	}

	private Task openCredentialConfiguration()
	{
		_credentialConfigurationDialog = new Dialog("Configure Credentials")
		{
			Width = Dim.Percent(50),
			Height = Dim.Percent(50)
		};

		Label accountEndpointLabel = new Label("Account Endpoint:")
		{
			X = 1,
			Y = 1
		};
		TextField accountEndpointField = new TextField(_appSettings.AccountEndpoint ?? "")
		{
			X = 1,
			Y = Pos.Bottom(accountEndpointLabel),
			Width = Dim.Fill()
		};

		Label accountKeyLabel = new Label("Account Key:")
		{
			X = 1,
			Y = Pos.Bottom(accountEndpointField) + 1
		};
		TextField accountKeyField = new TextField(_appSettings.AccountKey ?? "")
		{
			X = 1,
			Y = Pos.Bottom(accountKeyLabel),
			Width = Dim.Fill()
		};

		Button cancelButton = new Button("Cancel");
		cancelButton.Clicked += () => _credentialConfigurationDialog.RequestStop();

		Button saveButton = new Button("Save");
		saveButton.Clicked += async () =>
		{
			try
			{
				_cosmosApiWrapper.SetCredentials(accountEndpointField.Text.ToString(), accountKeyField.Text.ToString());
			}
			catch (ArgumentException e)
			{
				MessageBox.ErrorQuery("Error", e.ParamName + "is a required field.", "Ok");
				return;
			}
			catch (UriFormatException)
			{
				MessageBox.ErrorQuery("Error", "Invalid URI format.", "Ok");
				return;
			}

			if (!await _cosmosApiWrapper.VerifyConnection())
			{
				MessageBox.ErrorQuery("Error", "Invalid credentials.", "Ok");
				return;
			}

			_appSettings.AccountEndpoint = accountEndpointField.Text.ToString();
			_appSettings.AccountKey = accountKeyField.Text.ToString();
			await File.WriteAllTextAsync("appsettings.json", JsonSerializer.Serialize(_appSettings));

			_credentialConfigurationDialog.RequestStop();
		};

		_credentialConfigurationDialog.Add(accountEndpointLabel, accountEndpointField, accountKeyLabel,
										   accountKeyField);

		_credentialConfigurationDialog.AddButton(cancelButton);
		_credentialConfigurationDialog.AddButton(saveButton);

		Application.Run(_credentialConfigurationDialog);
		return Task.CompletedTask;
	}

	private async Task openDatabaseSelection()
	{
		_databaseSelectionDialog = new Dialog("Select Database")
		{
			Width = Dim.Percent(50),
			Height = Dim.Percent(50)
		};

		Button cancelButton = new Button("Cancel");
		cancelButton.Clicked += () => _databaseSelectionDialog.RequestStop();
		_databaseSelectionDialog.AddButton(cancelButton);

		try
		{
			List<DatabaseProperties> databases = await _cosmosApiWrapper.ListDatabases();

			foreach (DatabaseProperties database in databases)
			{
				Button button = new Button(database.Id)
				{
					Width = Dim.Fill()
				};
				button.Clicked += () =>
				{
					_appSettings.SelectedDatabase = database.Id;
					_cosmosApiWrapper.SelectDatabase(database.Id);
					File.WriteAllText("appsettings.json", JsonSerializer.Serialize(_appSettings));
					updateTitle();
					_databaseSelectionDialog.RequestStop();
				};

				_databaseSelectionDialog.Add(button);
			}
		}
		catch (InvalidOperationException e)
		{
			_databaseSelectionDialog.Text = e.Message;
		}

		Application.Run(_databaseSelectionDialog);
	}

	private async Task openContainerSelection()
	{
		_containerSelectionDialog = new Dialog("Select Container")
		{
			Width = Dim.Percent(50),
			Height = Dim.Percent(50)
		};

		Button cancelButton = new Button("Close");
		cancelButton.Clicked += () => _containerSelectionDialog.RequestStop();
		_containerSelectionDialog.AddButton(cancelButton);


		Application.Run(_containerSelectionDialog);
	}

	private async Task openColumnConfiguration()
	{
		_columnConfigurationDialog = new Dialog("Edit Columns")
		{
			Width = Dim.Percent(50),
			Height = Dim.Percent(50)
		};

		Button cancelButton = new Button("Close");
		cancelButton.Clicked += () => _columnConfigurationDialog.RequestStop();

		ListView listView = new ListView(_columns)
		{
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(1)
		};

		listView.OpenSelectedItem += (e) =>
		{
			TextField columnNameField = new TextField(e.Value.ToString())
			{
				X = 1,
				Y = 1,
				Width = Dim.Fill(),
			};

			Dialog columEditDialog = new Dialog("Edit Column")
			{
				Width = Dim.Percent(25),
				Height = 6
			};

			Button saveButton = new Button("Save");
			saveButton.Clicked += () =>
			{
				if (_columns.SkipWhile((c, i) => i == e.Item || c != columnNameField.Text.ToString()).Any())
				{
					MessageBox.ErrorQuery("Error", "Column already exists.", "Ok");
					return;
				}

				_columns[e.Item] = columnNameField.Text.ToString();
				_appSettings.Columns = _columns;
				updateTable();
				File.WriteAllText("appsettings.json", JsonSerializer.Serialize(_appSettings));
				columEditDialog.RequestStop();
			};

			columEditDialog.Add(columnNameField);
			columEditDialog.AddButton(saveButton);
			Application.Run(columEditDialog);
		};

		Button addButton = new Button("Add");
		addButton.Clicked += () =>
		{
			_columns.Add("New Column " + _columns.Count(c => c.StartsWith("New Column ")));
			listView.SelectedItem = _columns.Count - 1;
			listView.OnOpenSelectedItem();
		};

		Button removeButton = new Button("Remove");
		removeButton.Clicked += () =>
		{
			_columns.RemoveAt(listView.SelectedItem);
			updateTable();
		};

		Button removeAllButton = new Button("Remove All");
		removeAllButton.Clicked += () =>
		{
			_columns.Clear();
			updateTable();
		};

		Button moveUpButton = new Button("^");
		moveUpButton.Clicked += () =>
		{
			if (listView.SelectedItem == 0)
			{
				return;
			}

			(_columns[listView.SelectedItem], _columns[listView.SelectedItem - 1]) =
				(_columns[listView.SelectedItem - 1], _columns[listView.SelectedItem]);

			updateTable();

			listView.SelectedItem--;
			listView.SetFocus();
			updateTable();
		};

		Button moveDownButton = new Button("V");
		moveDownButton.Clicked += () =>
		{
			if (listView.SelectedItem == _columns.Count - 1)
			{
				return;
			}

			(_columns[listView.SelectedItem], _columns[listView.SelectedItem + 1]) =
				(_columns[listView.SelectedItem + 1], _columns[listView.SelectedItem]);
			listView.SelectedItem++;
			listView.SetFocus();
			updateTable();
		};

		_columnConfigurationDialog.Add(listView);
		_columnConfigurationDialog.AddButton(cancelButton);
		_columnConfigurationDialog.AddButton(addButton);
		_columnConfigurationDialog.AddButton(removeButton);
		_columnConfigurationDialog.AddButton(removeAllButton);
		_columnConfigurationDialog.AddButton(moveUpButton);
		_columnConfigurationDialog.AddButton(moveDownButton);
		Application.Run(_columnConfigurationDialog);
	}

	private void tableLoading()
	{
		_dt.Rows.Clear();

		if (_dt.Columns.Count == 0)
		{
			return;
		}

		_dt.Rows.Add("Loading");
	}

	private void updateTable()
	{
		_updating = true;
		_dt.Rows.Clear();
		_dt.Columns.Clear();

		foreach (string column in _columns)
		{
			_dt.Columns.Add(column);
		}

		if (_cosmosApiWrapper.Pages.Count <= _navigatorBar.CurrentPage)
		{
			_navigatorBar.SetPage(_cosmosApiWrapper.Pages.Count - 1);

			_dt.AcceptChanges();
			_resultsTable.Update();
			return;
		}

		foreach (dynamic entity in _cosmosApiWrapper.Pages[_navigatorBar.CurrentPage])
		{
			DataRow row = _dt.NewRow();

			foreach (string column in _columns)
			{
				try
				{
					row[column] = entity[column];
				}
				catch (RuntimeBinderException)
				{
					row[column] = "";
				}
			}

			_dt.Rows.Add(row);
		}

		_navigatorBar.UpdateButtons();
		_dt.AcceptChanges();
		_resultsTable.Update();
		_updating = false;
	}

	private void updateQueryField(string query = "")
	{
		if (_appSettings.SelectedContainer is null)
		{
			_queryInputView.Disable();
		}
		else
		{
			_queryInputView.Enable(query);
		}
	}

	private void updateTitle()
	{
		if (_appSettings.SelectedContainer is null)
		{
			if (_appSettings.SelectedDatabase is null)
			{
				Title = "Telescope";
				return;
			}

			Title = $"Telescope - {_appSettings.SelectedDatabase}";
			return;
		}

		Title = $"Telescope - {_appSettings.SelectedDatabase}/{_appSettings.SelectedContainer}";
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_cosmosApiWrapper.Dispose();
			_dt.Dispose();
		}

		base.Dispose(disposing);
	}
}