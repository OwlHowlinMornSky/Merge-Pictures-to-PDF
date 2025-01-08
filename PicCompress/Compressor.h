﻿#pragma once

using namespace System;

namespace PicCompress {

/**
 * @brief 图片压缩器。
 */
public ref class Compressor {
public:
	/**
	 * @brief 构造函数。
	 * @param houtfile: 输出 之 内存映射文件 之 原始句柄。
	 * @param oFileMaxLen: 输出 之 内存映射文件 之 最大大小（字节）。
	 */
	Compressor(System::IntPtr houtfile, System::Int64 oFileMaxLen);
	~Compressor();

public:
	/**
	 * @brief 压缩。将 把结果 写入 构造时指定之内存映射文件。
	 * @param pathOfInFile: 输入之文件的 路径。
	 * @return 写入输出文件之大小（字节）。
	 */
	System::Int32 Compress(System::String^ pathOfInFile, System::Int32 type, System::Int32 quality);

	/**
	 * @brief 压缩。从 指定之内存映射文件 读取，写入 构造时指定之内存映射文件。
	 * @param hinfile: 输入 之 内存映射文件 之 原始句柄。
	 * @param iFileLen: 输入 之 内存映射文件 之 大小（字节）。
	 * @return 写入输出文件之大小（字节）。
	 */
	System::Int32 CompressFrom(System::IntPtr hinfile, System::Int64 iFileLen, System::Int32 type, System::Int32 quality);

private:
	void*          m_viewOfOutFile; // 输出之文件映射。
	System::UInt64 m_oFileMaxLen;   // 输出之最大大小。
};

}
