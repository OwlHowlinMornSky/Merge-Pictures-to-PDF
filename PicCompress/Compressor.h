#pragma once

using namespace System;

namespace PicCompress {

/**
 * @brief 图片压缩器。
 */
public ref class Compressor {
public:
	/**
	 * @brief 构造函数。
	 * @param handle: 内存映射文件的原始句柄。
	 * @param maxlen: 内存映射文件的最大大小（字节）。
	 */
	Compressor(System::IntPtr handle, System::Int64 maxlen);
	~Compressor();

public:
	/**
	 * @brief 压缩。写入构造时指定的内存映射文件。
	 * @param file: 文件路径。
	 * @return 压缩后的大小（字节）。
	 */
	System::Int32 Compress(System::String^ file);

	/**
	 * @brief 压缩。从指定的内存映射读取，写入构造时指定的内存映射文件。
	 * @param handle: 内存映射文件的原始句柄。
	 * @param len: 内存映射文件的大小（字节）。
	 * @return 输出大小（字节）。
	 */
	System::Int32 CompressFrom(System::IntPtr handle, System::Int64 len);

private:
	void* m_view;      // 创建的文件映射。
	System::UInt64 m_maxlen; // 保存的最大大小。
};

}
