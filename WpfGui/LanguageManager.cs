using System.ComponentModel;
using System.Windows;

namespace WpfGui {
	public class LanguageManager : INotifyPropertyChanged {

		public static readonly int LangCnt = 2;

		public int CurrentLangId {
			get; set {
				if (value >= LangCnt)
					value %= LangCnt;
				if (value < 0)
					value = 0;
				ResourceDictionary rd = new() {
					Source = value switch {
						1 => new Uri("DictionaryMainGUI.zh-CN.xaml", UriKind.Relative),
						_ => new Uri("DictionaryMainGUI.xaml", UriKind.Relative),
					}
				};
				//App.Current.Resources.MergedDictionaries.Clear();
				App.Current.Resources.MergedDictionaries.Add(rd);

				// 先收集要移除的项（排除 rd）
				var toRemove = App.Current.Resources.MergedDictionaries
						  .Where(d => !ReferenceEquals(d, rd))
						  .ToList();

				foreach (var item in toRemove) {
					App.Current.Resources.MergedDictionaries.Remove(item);
				}

				field = value;
				OnPropertyChanged(nameof(CurrentLangId));
			}
		} = Settings1.Default.Language;

		public event PropertyChangedEventHandler? PropertyChanged;
		protected virtual void OnPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
