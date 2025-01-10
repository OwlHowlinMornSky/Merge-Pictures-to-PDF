using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfGui {
	/// <summary>
	/// WindowMorePreferences.xaml 的交互逻辑
	/// </summary>
	public partial class WindowMorePreferences : Window {
		public WindowMorePreferences(int _type, int _quality) {
			InitializeComponent();

			ComboBoxCompressType.SelectedIndex = _type switch {
				2 => 1,
				_ => 0,
			};
			SliderQuality.Value = _quality;
		}

		public string? CompressType {
			get {
				return (ComboBoxCompressType.SelectedItem as ComboBoxItem)?.Content.ToString();
			}
		}

		public int CompressQuility {
			get {
				return (int)SliderQuality.Value;
			}
		}

		private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (sender is Slider slider) {
				LabelSlideValue.Content = slider.Value.ToString();
			}
		}
	}
}
