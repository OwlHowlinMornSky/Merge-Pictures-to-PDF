using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfGui {

	public enum ImageFormat {
		TryKeep = 0,
		JPEG,
		PNG
	}

	public static class ImageFormatHelp {
		public static int StringToIndex(string item) {
			return (int)StringToEnum(item);
		}

		public static ImageFormat StringToEnum(string item) {
			if (Enum.TryParse(Settings1.Default.CompressFormat, true, out ImageFormat format)) {
				return format;
			}
			else {
				return ImageFormat.TryKeep;
			}
		}
	}

	public class ImageFormatDisplayConverter : IMultiValueConverter {
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
			if (values.Length > 0 && values[0] is ImageFormat format) {
				if (format == ImageFormat.TryKeep) {
					// 从应用程序资源中查找 "DetailCompressKeepType"
					if (Application.Current.TryFindResource("DetailCompressKeepType") is string display)
						return display;
				}
				// 其他枚举值直接返回其名称
				return format.ToString();
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
