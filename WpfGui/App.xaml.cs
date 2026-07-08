using System.IO;
using System.Reflection;
using System.Windows;

namespace WpfGui {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		public static LanguageManager LangMngr { get; } = new();

		protected override void OnStartup(StartupEventArgs e) {
			base.OnStartup(e);
			AppDomain.CurrentDomain.AssemblyResolve += ResolveAssemblyFromLibFolder;
		}

		private static Assembly? ResolveAssemblyFromLibFolder(object? sender, ResolveEventArgs args) {
			string? name = new AssemblyName(args.Name).Name;
			if (string.IsNullOrEmpty(name))
				return null;
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", $"{name}.dll");
			if (!File.Exists(path))
				return null;
			try {
				return Assembly.LoadFrom(path);
			}
			catch {
				return null;
			}
		}
	}
}
