
#include "BufferCompressor.h"

#include <libiodine/libiodine.h>

System::Int32 PicCompress::BufferCompressor::Compress(
	array<Byte>^% input,
	array<Byte>^% output,
	int targetFormat,
	int quality,
	bool resize,
	int width,
	int height,
	int shortSide,
	int longSide,
	bool reduceBtPowOf2
) {
	CSI_Parameters parameters = {};
	parameters.keep_metadata = false;
	parameters.jpeg_progressive = true;
	parameters.jpeg_quality = quality;
	parameters.png_quality = quality;
	if (resize) {
		parameters.width = width;
		parameters.height = height;
		parameters.short_side_pixels = shortSide;
		parameters.long_size_pixels = longSide;
		parameters.reduce_by_power_of_2 = reduceBtPowOf2;
		parameters.allow_magnify = false;
	}

	pin_ptr<Byte> inputBuffer(&input[0]);
	uint64_t inputLength = input->Length;
	void* inbuffer = inputBuffer;

	pin_ptr<Byte> outputBuffer(&output[0]);
	uint64_t outputMaxLength = output->Length;
	void* outbuffer = outputBuffer;
	 
	CSI_Result res;
	switch (targetFormat) {
	case 0:
		res = csi_compress_fromto(inbuffer, inputLength, outbuffer, outputMaxLength, &parameters);
		break;
	case 1:
		res = csi_convert_fromto(inbuffer, inputLength, outbuffer, outputMaxLength, CSI_SupportedFileTypes::Jpeg, &parameters);
		break;
	case 2:
		res = csi_convert_fromto(inbuffer, inputLength, outbuffer, outputMaxLength, CSI_SupportedFileTypes::Png, &parameters);
		break;
	default:
		throw gcnew System::ArgumentException(String::Format("Unknown target type: {0}", targetFormat));
	}

	if (!res.success) {
		throw gcnew InvalidOperationException(gcnew System::String(res.error_message));
	}
	if (res.code < 1 || res.code > 2147483600ull) {
		throw gcnew InsufficientMemoryException(String::Format("Code: {0}", res.code));
	}
	return (System::Int32)res.code;
}
