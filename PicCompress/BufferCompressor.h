#pragma once

#include "IodineOutputStream.h"

namespace PicCompress {

/**
 * @brief 图片压缩器。
 */
public ref class BufferCompressor {
public:
	/**
	 * @brief 压缩。从 指定之内存映射文件 读取，写入 构造时指定之内存映射文件。
	 * @param hinfile: 输入 之 内存映射文件 之 原始句柄。
	 * @param iFileLen: 输入 之 内存映射文件 之 大小（字节）。
	 * @param type: 压缩之目标类型。
	 * @param quality: 压缩之目标质量。
	 * @return 已写入输出文件 之 大小（字节）。
	 */
	static IodineOutputStream^ Compress(
		_In_ array<System::Byte>^ input,
		int targetFormat,
		int quality,
		bool optimize,
		bool resize,
		int width,
		int height
	);
};

}
