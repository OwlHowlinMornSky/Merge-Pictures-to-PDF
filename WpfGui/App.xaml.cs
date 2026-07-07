using System.Globalization;
using System.Windows;

namespace WpfGui {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		public static LanguageManager LangMngr { get; } = new();
	}

}
