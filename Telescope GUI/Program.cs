using Telescope_GUI;
using Terminal.Gui;

Application.Init();

try
{
	Application.Run<MainView>();
}
finally
{
    Application.Shutdown();
}