﻿#pragma once

using namespace System;

namespace PicCompress {

public ref class Compressor {
public:
	Compressor();
	~Compressor();

public:
	System::Void Compress(System::String^ file, System::IntPtr handle, System::Int64 maxlen);
};

}