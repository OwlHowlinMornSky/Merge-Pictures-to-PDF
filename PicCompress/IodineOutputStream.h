#pragma once

#include <libiodine.h>

using namespace System;
using namespace System::IO;
using namespace System::Runtime::InteropServices;

namespace PicCompress {

public ref class IodineOutputStream : public Stream {
private:
	Byte* m_buffer;    // 非托管缓冲区指针
	Int64 m_length;    // 缓冲区总大小（字节）
	Int64 m_position;  // 当前流位置

protected:
	virtual ~IodineOutputStream();
	!IodineOutputStream();

public:
	// 构造函数：包装现有非托管内存（不拥有）
	IodineOutputStream(CByteArray buffer);

	// 实现 Stream 抽象属性
	virtual property bool CanRead { bool get() override {
		return nullptr != m_buffer;
	} };
	virtual property bool CanSeek { bool get() override {
		return nullptr != m_buffer;
	} };
	virtual property bool CanWrite { bool get() override {
		return false;
	} };
	virtual property Int64 Length { Int64 get() override {
		return m_length;
	} };

	virtual property Int64 Position {
		Int64 get() override {
			return m_position;
		}
		void set(Int64 value) override {
			if (value < 0 || value > m_length)
				throw gcnew ArgumentOutOfRangeException("Position");
			m_position = value;
		}
	};

	// 读取数据到托管 byte[]（复制）
	virtual int Read(array<Byte>^ buffer, int offset, int count) override {
		if (nullptr == m_buffer)
			throw gcnew System::ObjectDisposedException("disposed");
		if (buffer == nullptr) throw gcnew ArgumentNullException("buffer");
		if (offset < 0 || count < 0) throw gcnew ArgumentOutOfRangeException();
		if (offset + count > buffer->Length) throw gcnew ArgumentException("Buffer too small");

		Int64 bytesToRead = Math::Min((Int64)count, m_length - m_position);
		if (bytesToRead <= 0) return 0;

		// 直接复制非托管内存到托管数组
		Marshal::Copy(IntPtr(m_buffer + m_position), buffer, offset, (int)bytesToRead);
		m_position += bytesToRead;
		return (int)bytesToRead;
	}

	// 写入数据（从托管 byte[]）
	virtual void Write(array<Byte>^ buffer, int offset, int count) override {
		/*只读流*/
		throw gcnew NotSupportedException("Stream does not support writing");
	}

	// 查找
	virtual Int64 Seek(Int64 offset, SeekOrigin origin) override {
		if (nullptr == m_buffer)
			throw gcnew System::ObjectDisposedException("disposed");
		Int64 newPos;
		switch (origin) {
		case SeekOrigin::Begin:   newPos = offset; break;
		case SeekOrigin::Current: newPos = m_position + offset; break;
		case SeekOrigin::End:     newPos = m_length + offset; break;
		default: throw gcnew ArgumentException("Invalid seek origin");
		}
		if (newPos < 0 || newPos > m_length)
			throw gcnew IOException("Seek position out of range");
		m_position = newPos;
		return m_position;
	}

	virtual void SetLength(Int64 value) override {
		// 不支持更改长度（若需支持，需重新分配内存并复制）
		throw gcnew NotSupportedException("Resizing is not supported");
	}

	virtual void Flush() override {
		/* 无操作，因为是内存流 */
		if (nullptr == m_buffer)
			throw gcnew System::ObjectDisposedException("disposed");
	}

	// 额外：提供索引器，方便按字节访问
	property Byte Item[Int64]{
		Byte get(Int64 index) {
			if (nullptr == m_buffer)
				throw gcnew System::ObjectDisposedException("disposed");
			if (index < 0 || index >= m_length)
				throw gcnew IndexOutOfRangeException();
			return m_buffer[index];
		}
		void set(Int64 index, Byte value) {
			if (nullptr == m_buffer)
				throw gcnew System::ObjectDisposedException("disposed");
			if (index < 0 || index >= m_length)
				throw gcnew IndexOutOfRangeException();
			m_buffer[index] = value;
		}
	};

	// 获取底层指针（谨慎使用）
	Byte* GetBufferPointer() {
		if (nullptr == m_buffer)
			throw gcnew System::ObjectDisposedException("disposed");
		return m_buffer;
	}
};

}
