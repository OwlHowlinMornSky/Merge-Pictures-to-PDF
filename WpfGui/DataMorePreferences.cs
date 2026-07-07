using System.ComponentModel;

namespace WpfGui {
	internal class MorePreference() : INotifyPropertyChanged {
		public bool IoMoveProcessed {
			get;
			set {
				Settings1.Default.IOMoveProcessed = value;
				field = value;
				OnPropertyChanged(nameof(IoMoveProcessed));
			}
		} = Settings1.Default.IOMoveProcessed;

		public bool IoPdfInFolder {
			get;
			set {
				Settings1.Default.IOPdfInFolder = value;
				field = value;
				OnPropertyChanged(nameof(IoPdfInFolder));
			}
		} = Settings1.Default.IOPdfInFolder;

		public static IEnumerable<ImageFormat> FormatList { get; } =
			Enum.GetValues<ImageFormat>().Cast<ImageFormat>();

		public ImageFormat TargetFormat {
			get;
			set {
				Settings1.Default.CompressFormat = value.ToString();
				field = value;
				OnPropertyChanged(nameof(TargetFormat));
			}
		} = ImageFormatHelp.StringToEnum(Settings1.Default.CompressFormat);

		public int Quality {
			get;
			set {
				Settings1.Default.CompressQuality = value;
				field = value;
				OnPropertyChanged(nameof(Quality));
			}
		} = int.Clamp(Settings1.Default.CompressQuality, 0, 100);

		public bool Resize {
			get;
			set {
				Settings1.Default.CompressResize = value;
				field = value;
				OnPropertyChanged(nameof(Resize));
			}
		} = Settings1.Default.CompressResize;

		public bool ResizeWidth {
			get;
			set {
				Settings1.Default.CompressResizeWidth = value;
				field = value;
				OnPropertyChanged(nameof(ResizeWidth));
			}
		} = Settings1.Default.CompressResizeWidth;

		public bool ResizeHeight {
			get;
			set {
				Settings1.Default.CompressResizeHeight = value;
				field = value;
				OnPropertyChanged(nameof(ResizeHeight));
			}
		} = Settings1.Default.CompressResizeHeight;

		public bool ResizeShort {
			get;
			set {
				Settings1.Default.CompressResizeShort = value;
				field = value;
				OnPropertyChanged(nameof(ResizeShort));
			}
		} = Settings1.Default.CompressResizeShort;

		public bool ResizeLong {
			get;
			set {
				Settings1.Default.CompressResizeLong = value;
				field = value;
				OnPropertyChanged(nameof(ResizeLong));
			}
		} = Settings1.Default.CompressResizeLong;

		public bool ResizeReduceByPow2 {
			get;
			set {
				Settings1.Default.CompressResizeReduceByPow2 = value;
				field = value;
				OnPropertyChanged(nameof(ResizeReduceByPow2));
			}
		} = Settings1.Default.CompressResizeReduceByPow2;

		public string ResizeWidthValue {
			get; set {
				Settings1.Default.CompressResizeWidthValue = int.Parse(value);
				field = value;
				OnPropertyChanged(nameof(ResizeWidthValue));
			}
		} = Settings1.Default.CompressResizeWidthValue.ToString();

		public string ResizeHeightValue {
			get; set {
				Settings1.Default.CompressResizeHeightValue = int.Parse(value);
				field = value;
				OnPropertyChanged(nameof(ResizeHeightValue));
			}
		} = Settings1.Default.CompressResizeHeightValue.ToString();

		public string ResizeShortValue {
			get; set {
				Settings1.Default.CompressResizeShortValue = int.Parse(value);
				field = value;
				OnPropertyChanged(nameof(ResizeShortValue));
			}
		} = Settings1.Default.CompressResizeShortValue.ToString();

		public string ResizeLongValue {
			get; set {
				Settings1.Default.CompressResizeLongValue = int.Parse(value);
				field = value;
				OnPropertyChanged(nameof(ResizeLongValue));
			}
		} = Settings1.Default.CompressResizeLongValue.ToString();



		public event PropertyChangedEventHandler? PropertyChanged;
		protected virtual void OnPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
