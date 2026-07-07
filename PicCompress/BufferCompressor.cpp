
#include "BufferCompressor.h"

#include <libiodine.h>

#include <stdlib.h>
#include <string.h>
#include <msclr/marshal.h>

PicCompress::IodineBufferViewer^ PicCompress::BufferCompressor::Compress(
	_In_ array<System::Byte>^ input,
	int targetFormat,
	int quality,
	bool resize,
	int width,
	int height
) {
	pin_ptr<System::Byte> input_bytes(&input[0]);
	uintptr_t input_buffer_len = input->Length;
	const byte* input_buffer = input_bytes;

	CCSParameters parameters = {};
	parameters.keep_metadata = false;
	parameters.jpeg_quality = quality;
	parameters.jpeg_progressive = true;
	parameters.png_quality = quality;
	parameters.gif_quality = quality;
	parameters.webp_quality = quality;
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
		throw gcnew System::ArgumentException(System::String::Format("Unknown target type: {0}", targetFormat));
	}

	if (!res.success) {
		auto str = msclr::interop::marshal_as<System::String^>(res.error_message);
		c_free_string((char*)res.error_message);
		throw gcnew System::InvalidOperationException(str);
	}

	return gcnew PicCompress::IodineBufferViewer(output_buffer);
}
