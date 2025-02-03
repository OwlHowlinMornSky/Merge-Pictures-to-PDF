using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfGui {
	/// <summary>
	/// WindowMorePreferences.xaml 的交互逻辑
	/// </summary>
	public partial class WindowMorePreferences : Window {
		protected bool Started {
			get; private set;
		} = false;
		public WindowMorePreferences() {
			InitializeComponent();
#if !DEBUG
			gpBoxDebug.Visibility = Visibility.Hidden;
#endif

			comboBoxCompressType.SelectedIndex = int.Clamp(Settings1.Default.CompressType, 0, comboBoxCompressType.Items.Count - 1);

			sliderQuality.Value = double.Clamp(Settings1.Default.CompressQuality, 0, 100);

			chkBoxResize.IsChecked = Settings1.Default.CompressResize;

			chkBoxWidth.IsChecked = Settings1.Default.CompressResizeWidth;
			chkBoxHeight.IsChecked = Settings1.Default.CompressResizeHeight;
			chkBoxShort.IsChecked = Settings1.Default.CompressResizeShort;
			chkBoxLong.IsChecked = Settings1.Default.CompressResizeLong;

			if (Settings1.Default.CompressResizeWidthValue == 0)
				Settings1.Default.CompressResizeWidthValue = (int)(Settings1.Default.PageSizeWidth * 4);
			if (Settings1.Default.CompressResizeHeightValue == 0)
				Settings1.Default.CompressResizeHeightValue = (int)(Settings1.Default.PageSizeHeight * 4);
			if (Settings1.Default.CompressResizeShortValue == 0)
				Settings1.Default.CompressResizeShortValue = (int)(Settings1.Default.PageSizeWidth * 4);
			if (Settings1.Default.CompressResizeLongValue == 0)
				Settings1.Default.CompressResizeLongValue = (int)(Settings1.Default.PageSizeHeight * 4);
			textBoxWidth.Text = Settings1.Default.CompressResizeWidthValue.ToString();
			textBoxHeight.Text = Settings1.Default.CompressResizeHeightValue.ToString();
			textBoxShort.Text = Settings1.Default.CompressResizeShortValue.ToString();
			textBoxLong.Text = Settings1.Default.CompressResizeLongValue.ToString();

			Started = true;
		}

		private void ComboBoxCompressType_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (!Started)
				return;
			if (comboBoxCompressType.SelectedIndex < 0 || comboBoxCompressType.SelectedIndex >= comboBoxCompressType.Items.Count) {
				MessageBox.Show(this, $"Failed to set compression type ({(comboBoxCompressType.SelectedItem as ComboBoxItem)?.Content}). Default is used no change.", Title);
				comboBoxCompressType.SelectedIndex = 0;
			}
			Settings1.Default.CompressType = comboBoxCompressType.SelectedIndex;
		}

		private void SliderQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (!Started)
				return;
			Settings1.Default.CompressQuality = int.Clamp((int)double.Round(sliderQuality.Value), 0, 100);
		}

		/// <summary>
		/// 输入尺寸的框 的 键入通知。用来限制 只能输入数字。
		/// </summary>
		private void TextNum_PreviewKeyDown(object sender, KeyEventArgs e) {
			bool isNum = e.Key >= Key.D0 && e.Key <= Key.D9;
			bool isNumPad = e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9;
			bool isControl = e.Key == Key.Back || e.Key == Key.Enter || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right;
			if (isNum || isNumPad || isControl) {
				return;
			}
			e.Handled = true;
		}

		private void ResizeCheckedChanged(object sender, RoutedEventArgs e) {
			chkBoxWidth.IsEnabled = chkBoxResize.IsChecked == true;
			chkBoxHeight.IsEnabled = chkBoxResize.IsChecked == true;
			chkBoxShort.IsEnabled = chkBoxResize.IsChecked == true;
			chkBoxLong.IsEnabled = chkBoxResize.IsChecked == true;

			textBoxWidth.IsEnabled = chkBoxWidth.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxHeight.IsEnabled = chkBoxHeight.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxShort.IsEnabled = chkBoxShort.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxLong.IsEnabled = chkBoxLong.IsChecked == true && chkBoxResize.IsChecked == true;

			if (!Started)
				return;
			Settings1.Default.CompressResize = chkBoxResize.IsChecked == true;
		}

		private bool m_isCheckChanging = false;
		private void ResizeWHCheckedChanged(object sender, RoutedEventArgs e) {
			if (m_isCheckChanging)
				return;
			m_isCheckChanging = true;

			if (chkBoxWidth.IsChecked == true || chkBoxHeight.IsChecked == true) {
				chkBoxShort.IsChecked = false;
				chkBoxLong.IsChecked = false;
			}

			textBoxWidth.IsEnabled = chkBoxWidth.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxHeight.IsEnabled = chkBoxHeight.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxShort.IsEnabled = chkBoxShort.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxLong.IsEnabled = chkBoxLong.IsChecked == true && chkBoxResize.IsChecked == true;

			m_isCheckChanging = false;
			if (!Started)
				return;
			Settings1.Default.CompressResizeWidth = chkBoxWidth.IsChecked == true;
			Settings1.Default.CompressResizeHeight = chkBoxHeight.IsChecked == true;
			Settings1.Default.CompressResizeShort = chkBoxShort.IsChecked == true;
			Settings1.Default.CompressResizeLong = chkBoxLong.IsChecked == true;
		}

		private void ResizeSIDECheckedChanged(object sender, RoutedEventArgs e) {
			if (m_isCheckChanging)
				return;
			m_isCheckChanging = true;

			if (chkBoxShort.IsChecked == true || chkBoxLong.IsChecked == true) {
				chkBoxWidth.IsChecked = false;
				chkBoxHeight.IsChecked = false;
			}

			textBoxWidth.IsEnabled = chkBoxWidth.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxHeight.IsEnabled = chkBoxHeight.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxShort.IsEnabled = chkBoxShort.IsChecked == true && chkBoxResize.IsChecked == true;
			textBoxLong.IsEnabled = chkBoxLong.IsChecked == true && chkBoxResize.IsChecked == true;

			m_isCheckChanging = false;
			if (!Started)
				return;
			Settings1.Default.CompressResizeWidth = chkBoxWidth.IsChecked == true;
			Settings1.Default.CompressResizeHeight = chkBoxHeight.IsChecked == true;
			Settings1.Default.CompressResizeShort = chkBoxShort.IsChecked == true;
			Settings1.Default.CompressResizeLong = chkBoxLong.IsChecked == true;
		}

		private void ResizeTextChanged(object sender, TextChangedEventArgs e) {
			if (!Started)
				return;
			Settings1.Default.CompressResizeWidthValue = int.TryParse(textBoxWidth.Text, out int res) ? res : 0;
			Settings1.Default.CompressResizeHeightValue = int.TryParse(textBoxHeight.Text, out res) ? res : 0;
			Settings1.Default.CompressResizeShortValue = int.TryParse(textBoxShort.Text, out res) ? res : 0;
			Settings1.Default.CompressResizeLongValue = int.TryParse(textBoxLong.Text, out res) ? res : 0;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			Settings1.Default.Save();
		}

		private void ButtonResetSettings_Click(object sender, RoutedEventArgs e) {
			Settings1.Default.Reset();
		}

		private void ButtonLanguage_Click(object sender, RoutedEventArgs e) {
			Settings1.Default.Language = Settings1.Default.Language == 0 ? 1 : 0;
			MainWindow.ChangeLang(Settings1.Default.Language);
		}

	}
}
