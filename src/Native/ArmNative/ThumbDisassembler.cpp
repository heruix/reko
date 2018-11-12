/*
* Copyright (C) 1999-2018 John Källén.
*
* This program is free software; you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation; either version 2, or (at your option)
* any later version.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program; see the file COPYING.  If not, write to
* the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
*/

#include "stdafx.h"

#include "reko.h"

#include "functions.h"
#include "ComBase.h"
#include "NativeInstruction.h"
#include "ThumbDisassembler.h"
#include "ArmArchitecture.h"

ThumbDisassembler::ThumbDisassembler(const uint8_t * bytes, size_t length, int offset, uint64_t uAddr) :
	bytes(bytes), length(length), offset(offset), uAddr(uAddr)
{
	auto ec = cs_open(CS_ARCH_ARM, CS_MODE_THUMB, &hcapstone);
	ec = cs_option(this->hcapstone, CS_OPT_DETAIL, CS_OPT_ON);
}

ThumbDisassembler::~ThumbDisassembler()
{
	Dump("Destroying ThumbDisassembler");
}

HRESULT STDAPICALLTYPE ThumbDisassembler::QueryInterface(REFIID iid, void ** ppvOut)
{
	if (iid == IID_INativeDisassembler ||
		iid == IID_IAgileObject ||
		iid == IID_IUnknown)
	{
		AddRef();
		*ppvOut = static_cast<INativeDisassembler *>(this);
		return S_OK;
	}
	ppvOut = nullptr;
	return E_NOINTERFACE;
}

INativeInstruction * ThumbDisassembler::NextInstruction()
{
	if (length == 0)
	{
		return nullptr;
	}
	uint64_t uAddr = this->uAddr;
	auto instr = cs_malloc(hcapstone);
	if (!cs_disasm_iter(hcapstone, &this->bytes, &this->length, &this->uAddr, instr))
	{
		instr->detail->arm.op_count = 0;
		auto info = NativeInstructionInfo{
			uAddr, 4, static_cast<uint32_t>(InstrClass::Invalid), ARM_INS_INVALID
		};
		this->uAddr += 4;
		this->length -= 4;
		return new NativeInstruction(instr, info);
	}
	else
	{
		auto info = NativeInstructionInfo{
			uAddr, 4,
			static_cast<uint32_t>(InstrClass::Linear), 
			static_cast<int32_t>(instr->id)
		};
		return new NativeInstruction(instr, info);
	}
}

inline InstrClass operator | (InstrClass a, InstrClass b) {
	return static_cast<InstrClass>(static_cast<int>(a) | static_cast<int>(b));
}

InstrClass ThumbDisassembler::InstructionClassFromId(unsigned int armInstrID)
{
	switch (armInstrID)
	{
	case ARM_INS_INVALID: return InstrClass::Invalid;
	case ARM_INS_BKPT: return InstrClass::Transfer;
	case ARM_INS_BL: return InstrClass::Transfer | InstrClass::Call;
	case ARM_INS_BLX: return InstrClass::Transfer | InstrClass::Call;
	case ARM_INS_BX: return InstrClass::Transfer;
	case ARM_INS_BXJ: return InstrClass::Transfer;
	case ARM_INS_B: return InstrClass::Transfer;
	case ARM_INS_HLT: return InstrClass::Transfer;
	case ARM_INS_SVC: return InstrClass::Transfer;
	case ARM_INS_TEQ: return InstrClass::Transfer;
	case ARM_INS_TRAP: return InstrClass::Transfer;
	case ARM_INS_YIELD: return InstrClass::Transfer;
	}
	return InstrClass::Linear;
}

