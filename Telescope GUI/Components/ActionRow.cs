using Terminal.Gui;

namespace Telescope_GUI;

public partial class MainView
{
	private Button _deleteButton;
	private Button _deleteAllButton;

	private void initialiseActionRow()
	{
		_deleteButton = new Button("Delete")
		{
			X = 0,
			Y = Pos.Bottom(_queryInputView) + 1
		};

		_deleteAllButton = new Button("Delete All")
		{
			X = Pos.Right(_deleteButton),
			Y = Pos.Bottom(_queryInputView) + 1
		};
		
	}
}