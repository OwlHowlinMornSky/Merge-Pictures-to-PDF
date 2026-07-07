using System.Buffers;
using System.ComponentModel;

namespace WpfGui {
	internal class DataMain : INotifyPropertyChanged {

		public IEnumerable<int> FormatList {
			get; private set {
				field = value;
				OnPropertyChanged(nameof(FormatList));
			}
		}

		public int PageSizeId {
			get;
			set {
				Settings1.Default.PagePageType = value;
				field = value;
				OnPropertyChanged(nameof(PageSizeId));
				if (value != 0) {
					var nsize = PageSizeDisplayConverter.GetPageSize(value);
					// Trigger Event
					PageSizeWidth++;
					PageSizeWidth = nsize.Width;
					PageSizeHeight++;
					PageSizeHeight = nsize.Height;
					PageSizeDpi++;
					PageSizeDpi = 72.0;
				}
			}
		} = Settings1.Default.PagePageType;

		public double PageSizeWidth {
			get; set {
				Settings1.Default.PageSizeWidth = value;
				field = value;
				OnPropertyChanged(nameof(PageSizeWidth));
			}
		} = Settings1.Default.PageSizeWidth;

		public double PageSizeHeight {
			get; set {
				Settings1.Default.PageSizeHeight = value;
				field = value;
				OnPropertyChanged(nameof(PageSizeHeight));
			}
		} = Settings1.Default.PageSizeHeight;

		public double PageSizeDpi {
			get; set {
				Settings1.Default.PageDpi = value;
				field = value;
				OnPropertyChanged(nameof(PageSizeDpi));
			}
		} = Settings1.Default.PageDpi;

		public bool PageFixedWidth {
			get;
			set {
				Settings1.Default.PageFixedWidth = value;
				field = value;
				OnPropertyChanged(nameof(PageFixedWidth));
			}
		} = Settings1.Default.PageFixedWidth;

		public bool PageFixedHeight {
			get;
			set {
				Settings1.Default.PageFixedHeight = value;
				field = value;
				OnPropertyChanged(nameof(PageFixedHeight));
			}
		} = Settings1.Default.PageFixedHeight;

		public bool IORecurse {
			get;
			set {
				Settings1.Default.IORecurse = value;
				field = value;
				OnPropertyChanged(nameof(IORecurse));
			}
		} = Settings1.Default.IORecurse;

		public bool IOKeepStruct {
			get;
			set {
				Settings1.Default.IOKeepStruct = value;
				field = value;
				OnPropertyChanged(nameof(IOKeepStruct));
			}
		} = Settings1.Default.IOKeepStruct;

		public bool IOCompress {
			get;
			set {
				Settings1.Default.IOCompress = value;
				field = value;
				OnPropertyChanged(nameof(IOCompress));
			}
		} = Settings1.Default.IOCompress;

		public bool IONoMove {
			get;
			set {
				Settings1.Default.IONoMove = value;
				field = value;
				OnPropertyChanged(nameof(IONoMove));
			}
		} = Settings1.Default.IONoMove;


		public DataMain() {
			PageSizeDisplayConverter.Reset();
			List<int> list = [];
			list.Add(0);
			int index = 1;
			foreach (string paperType in Settings1.Default.Papers.Split(',')) {
				try {
					var obj = Settings1.Default[$"Paper{paperType}"];
					if (obj is System.Drawing.Size _size) {
						PageSizeDisplayConverter.AddPageType(index, paperType, _size);
						list.Add(index);
						index++;
					}
				}
				catch {
					;
				}
			}
			FormatList = list;
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		protected virtual void OnPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
