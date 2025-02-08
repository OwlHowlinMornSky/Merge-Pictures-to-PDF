
#include "BufferCompressor.h"

#include <libiodine/libiodine.h>

System::Int32 PicCompress::BufferCompressor::Compress(
	array<Byte>^% input,
	array<Byte>^% output,
	CompressParam param
) {
	CSI_Parameters parameters = {};
	parameters.keep_metadata = false;
	parameters.jpeg_progressive = true;
	parameters.jpeg_quality = param.quality;
	parameters.png_quality = param.quality;
	if (param.resize) {
		parameters.width = param.width;
		parameters.height = param.height;
		parameters.short_side_pixels = param.shortSide;
		parameters.long_size_pixels = param.longSide;
		parameters.reduce_by_power_of_2 = param.reduceBtPowOf2;
		parameters.allow_magnify = false;
	}

	pin_ptr<Byte> inputBuffer(&input[0]);
	uint64_t inputLength = input->Length;
	void* inbuffer = inputBuffer;

	pin_ptr<Byte> outputBuffer(&output[0]);
	uint64_t outputMaxLength = output->Length;
	void* outbuffer = outputBuffer;

	CSI_Result res;
	switch (param.targetFormat) {
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
		throw gcnew System::ArgumentException(String::Format("Unknown target type: {0}", param.targetFormat));
	}

	if (!res.success) {
		throw gcnew InvalidOperationException(gcnew System::String(res.error_message));
	}
	if (res.code < 1 || res.code > 2147483600ull) {
		throw gcnew InsufficientMemoryException(String::Format("Code: {0}", res.code));
	}
	return (System::Int32)res.code;
}
