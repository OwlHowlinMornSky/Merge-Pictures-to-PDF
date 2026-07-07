
#include "BufferCompressor.h"

#include <libiodine.h>

namespace {

class IodineString {
	const char* _msg;
public:
	IodineString(const char* msg) :
		_msg(msg) {
	}

	~IodineString() {
		if (nullptr != _msg)
			c_free_string((char*)_msg);
		_msg = nullptr;
	}

	const char* get() const {
		return _msg;
	}
};

}

PicCompress::IodineOutputStream^ PicCompress::BufferCompressor::Compress(
	_In_ array<System::Byte>^ input,
	int targetFormat,
	int quality,
	bool optimize,
	bool resize,
	int width,
	int height
) {
	pin_ptr<System::Byte> input_bytes(&input[0]);
	uintptr_t input_buffer_len = input->Length;
	const Byte* input_buffer = input_bytes;

	CCSParameters parameters = {};

	parameters.keep_metadata = false;

	parameters.jpeg_quality = quality;
	parameters.jpeg_progressive = true;
	parameters.jpeg_optimize = optimize;

	parameters.png_quality = quality;
	parameters.png_optimize = optimize;

	parameters.gif_quality = quality;

	parameters.webp_quality = quality;
	parameters.webp_lossless = optimize;

	if (resize) {
		parameters.width = width;
		parameters.height = height;
	}

	CByteArray output_buffer{};
	CCSResult res{};
	switch (targetFormat) {
	case 0:
		res = iod_compress_in_memory(input_buffer, input_buffer_len, parameters, &output_buffer);
		break;
	case 1:
		res = iod_convert_in_memory(input_buffer, input_buffer_len, SupportedFileTypes::Jpeg, parameters, &output_buffer);
		break;
	case 2:
		res = iod_convert_in_memory(input_buffer, input_buffer_len, SupportedFileTypes::Png, parameters, &output_buffer);
		break;
	default:
		throw gcnew System::ArgumentException(System::String::Format("Unknown target type: {0}.", targetFormat));
	}
	IodineString err_str{ res.error_message };

	if (!res.success) {
		if (err_str.get() != nullptr) {
			System::String^ str = Marshal::PtrToStringAnsi((System::IntPtr)(void*)err_str.get());
			throw gcnew System::InvalidOperationException(
				System::String::Format(
					"Iodine error ({0}): {1}, target: {2}.",
					res.code, str, targetFormat
				)
			);
		}
		throw gcnew System::InvalidOperationException(
			System::String::Format(
				"Iodine error ({0}), target: {1}.",
				res.code, targetFormat
			)
		);
	}

	return gcnew PicCompress::IodineOutputStream(output_buffer);
}
