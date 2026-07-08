using System.Globalization;
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

			if (string.IsNullOrEmpty(name)) {
				return null;
			}
			if (name.EndsWith(".resources")) {
				return TryLoadResource(name);
			}
			else {
				return TryLoadAssembly(name);
			}
		}

		private static Assembly? TryLoadAssembly(string name) {
			var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", $"{name}.dll");
			return TryLoad(path);
		}

		private static Assembly? TryLoadResource(string name) {
			CultureInfo culture;
			string path;
			culture = Thread.CurrentThread.CurrentUICulture;
			do {
				var old_culture = culture;
				path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", old_culture.Name, $"{name}.dll");
				if (TryLoad(path) is Assembly res)
					return res;
				var new_culture = old_culture.Parent;
				if (old_culture == new_culture)
					break;
				culture = new_culture;
			} while (true);
			culture = Thread.CurrentThread.CurrentUICulture;
			do {
				var old_culture = culture;
				path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, old_culture.Name, $"{name}.dll");
				if (TryLoad(path) is Assembly res)
					return res;
				var new_culture = old_culture.Parent;
				if (old_culture == new_culture)
					break;
				culture = new_culture;
			} while (true);
			return null;
		}

		private static Assembly? TryLoad(string path) {
			if (!File.Exists(path)) {
				return null;
			}
			try {
				return Assembly.LoadFrom(path);
			}
			catch {
				return null;
			}
		}

	}
}
