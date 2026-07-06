#pragma once

#include <libiodine.h>

public ref class IodineBufferViewer : System::IDisposable {
private:
	System::Byte* m_buffer;
	int m_length;

public:
	IodineBufferViewer(CByteArray buffer);
	~IodineBufferViewer();
	!IodineBufferViewer();

	property int Length{
		int get() {
			return m_length;
		}
	};

	property System::Byte Item[int]{
		System::Byte get(int index) {
			if (nullptr == m_buffer)
				throw gcnew System::ObjectDisposedException("This IodineBufferViewer is disposed");
			if (index < 0 || index >= m_length)
				throw gcnew System::IndexOutOfRangeException("When reading a buffer which comes from iodine");
			return m_buffer[index];
		}
		void set(int index, System::Byte value) {
			if (nullptr == m_buffer)
				throw gcnew System::ObjectDisposedException("This IodineBufferViewer is disposed");
			if (index < 0 || index >= m_length)
				throw gcnew System::IndexOutOfRangeException("When writing a buffer which comes from iodine");
			m_buffer[index] = value;
		}
	};

	// 提供直接访问本机指针的方法
	System::Byte* GetNativePointer();
};

