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

	private CosmosApiWrapper _cosmosApiWrapper;
	private AppSettings _appSettings;

	private Dialog? _credentialConfigurationDialog;
	private Dialog? _databaseSelectionDialog;
	private Dialog? _containerSelectionDialog;
	
	private List<string> _columns = ["id", "UserName", "Type"];

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
		_cosmosApiWrapper.PageSize = _appSettings.PageSize ?? 25;

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
								 async () => await credentialConfigurationClick()),
					new MenuItem("_Select Database", "", async () => await databaseSelectionClick()),
					new MenuItem("_Select Container", "", async () => await containerSelectionClick()),
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
		_dt.Columns.Add("Id");

		_resultsTable = new TableView
		{
			X = 0,
			Y = Pos.Bottom(_queryInputView) + 1,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Table = _dt,
			Style = new TableView.TableStyle
			{
				AlwaysShowHeaders = true
			}
		};

		_queryInputView.QueryField.Text = _appSettings.LastQuery;
		updateQueryField();
		Add(_menuBar, _queryInputView, _resultsTable);
	}

	private async Task executeQuery()
	{
		if (_loading)
		{
			return;
		}

		_loading = true;
		_dt.Rows.Clear();
		_dt.Rows.Add("Loading");

		try
		{
			await _cosmosApiWrapper.GetFirstPageByQueryAsync(_queryInputView.QueryField.Text.ToString());
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

	private Task credentialConfigurationClick()
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

	private async Task databaseSelectionClick()
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

	private async Task containerSelectionClick()
	{
		_containerSelectionDialog = new Dialog("Select Container")
		{
			Width = Dim.Percent(50),
			Height = Dim.Percent(50)
		};

		Button cancelButton = new Button("Cancel");
		cancelButton.Clicked += () => _containerSelectionDialog.RequestStop();
		_containerSelectionDialog.AddButton(cancelButton);

		try
		{
			List<ContainerProperties> containers = await _cosmosApiWrapper.ListContainers();

			foreach (ContainerProperties container in containers)
			{
				Button button = new Button(container.Id)
				{
					Width = Dim.Fill()
				};
				button.Clicked += () =>
				{
					_appSettings.SelectedContainer = container.Id;
					_cosmosApiWrapper.SelectContainer(container.Id);
					File.WriteAllText("appsettings.json", JsonSerializer.Serialize(_appSettings));
					updateTitle();
					updateQueryField();
					_containerSelectionDialog.RequestStop();
				};

				_containerSelectionDialog.Add(button);
			}
		}
		catch (InvalidOperationException e)
		{
			_containerSelectionDialog.Text = e.Message;
		}

		Application.Run(_containerSelectionDialog);
	}

	private void updateTable()
	{
		_dt.Rows.Clear();
		_dt.Columns.Clear();

		foreach (string column in _columns)
		{
			_dt.Columns.Add(column);
		}
		
		if (_cosmosApiWrapper.Pages.Count == 0)
		{
			_dt.AcceptChanges();
			_resultsTable.Update();
			return;
		}

		foreach (dynamic entity in _cosmosApiWrapper.Pages[0])
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

		_dt.AcceptChanges();
		_resultsTable.Update();
	}

	private void updateQueryField()
	{
		if (_appSettings.SelectedContainer is null)
		{
			_queryInputView.Disable();
		}
		else
		{
			_queryInputView.Enable();
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