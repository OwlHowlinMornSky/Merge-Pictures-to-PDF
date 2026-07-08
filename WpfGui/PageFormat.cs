using System.Globalization;
using System.Windows.Data;

namespace WpfGui {

	public class PageSizeDisplayConverter : IMultiValueConverter {
		public struct PageTypeInfo(string _1, System.Drawing.Size _2) {
			public string name = _1;
			public System.Drawing.Size size = _2;
		}

		private static Dictionary<int, PageTypeInfo> page_types = [];

		public static void Reset() {
			page_types.Clear();
		}

		public static void AddPageType(int id, string name, System.Drawing.Size size) {
			page_types.Add(id, new PageTypeInfo(name, size));
		}

		public static System.Drawing.Size GetPageSize(int id) {
			if (page_types.TryGetValue(id, out var v)) {
				return v.size;
			}
			throw new ArgumentOutOfRangeException($"Page id '{id}' is invalid. Failed to get size.");
		}

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
			if (values.Length > 0 && values[0] is int type) {
				switch (type) {
				case 0:
					var n = App.Current.TryFindResource("PageSizePaperCustom");
					if (n is string nv)
						return nv;
					break;
				default:
					if (page_types.TryGetValue(type, out var v)) {
						return v.name;
					}
					break;
				}
				return type.ToString();
			}
			return Binding.DoNothing;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
			// 不需要实现，因为 ComboBox 的 SelectedItem 绑定直接操作枚举对象，
			// 不会调用 ConvertBack。
			throw new NotImplementedException();
		}
	}

}
