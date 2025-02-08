﻿#pragma once

using namespace System;

namespace PicCompress {

public ref struct CompressParam {
	int targetFormat;
	int quality;
	bool resize;
	int width;
	int height;
	int shortSide;
	int longSide;
	bool reduceBtPowOf2;
};

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
	static System::Int32 Compress(
		[Runtime::InteropServices::InAttribute] array<Byte>^% input,
		array<Byte>^% output,
		CompressParam param
	);
};

}
