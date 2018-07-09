﻿using BizHawk.Common;
using BizHawk.Common.NumberExtensions;
using System;
using System.Collections;

namespace BizHawk.Emulation.Cores.Computers.AmstradCPC
{
    /// <summary>
    /// Emulates the PPI (8255) chip
    /// http://www.cpcwiki.eu/imgs/d/df/PPI_M5L8255AP-5.pdf
    /// http://www.cpcwiki.eu/index.php/8255
    /// </summary>
    public class PPI_8255 : IPortIODevice
    {
        #region Devices

        private CPCBase _machine;
        private CRCT_6845 CRTC => _machine.CRCT;
        private AmstradGateArray GateArray => _machine.GateArray;
        private IPSG PSG => _machine.AYDevice;
        private DatacorderDevice Tape => _machine.TapeDevice;
        private IKeyboard Keyboard => _machine.KeyboardDevice;

        #endregion

        #region State

        private State PortA = new State("PortA");
        private State PortB = new State("PortB");
        private State PortCU = new State("PortCU");
        private State PortCL = new State("PortCL");

        private class State
        {
            public string Ident;
            public int Data;
            public bool Input;
            public int OpMode;

            public void Reset()
            {
                OpMode = 0;
                Input = true;
                Data = 255;
            }

            public State(string ident)
            {
                Ident = ident;
            }

            public void SyncState(Serializer ser)
            {
                ser.BeginSection("PPI_" + Ident);
                ser.Sync("Data", ref Data);
                ser.Sync("Input", ref Input);
                ser.Sync("OpMode", ref OpMode);
                ser.EndSection();
            }
        }

        #endregion

        #region Construction

        public PPI_8255(CPCBase machine)
        {
            _machine = machine;
            Reset();
        }

        #endregion

        #region Reset

        public void Reset()
        {
            PortA.Reset();
            PortB.Reset();
            PortCL.Reset();
            PortCU.Reset();
        }

        #endregion

        #region PORT A
        /*
        I/O Mode 0,
        For writing data to PSG all bits must be set to output,
        for reading data from PSG all bits must be set to input (thereafter, output direction should be restored, for compatibility with the BIOS).

        Bit	    Description	Usage
        7-0	    PSG.DATA	PSG Databus (Sound/Keyboard/Joystick)
        */
        /// <summary>
        /// Reads from Port A
        /// </summary>
        /// <returns></returns>
        private int INPortA()
        {
            if (PortA.Input)
            {
                // read from AY
                return PSG.PortRead();
            }
            else
            {
                // return stored port data
                return PortA.Data;
            }
        }

        /// <summary>
        /// Writes to Port A
        /// </summary>
        private void OUTPortA(int data)
        {
            PortA.Data = data;

            if (!PortA.Input)
            {
                // write to AY
                PSG.PortWrite(data);
            }
        }

        #endregion

        #region Port B
        /*
        I/O Mode 0,
        Input

        Bit	Description	Usage in CPC	Usage in KC Compact
        7	CAS.IN	    Cassette data input	Same as on CPC
        6	PRN.BUSY	Parallel/Printer port ready signal, "1" = not ready, "0" = Ready	Same as on CPC
        5	/EXP	    Expansion Port /EXP pin	Same as on CPC
        4	LK4	        Screen Refresh Rate ("1"=50Hz, "0"=60Hz)	Set to "1"=50Hz (but ignored by the KC BIOS, which always uses 50Hz even if LK4 is changed)
        3	LK3	        3bit Distributor ID. Usually set to 4=Awa, 5=Schneider, or 7=Amstrad, see LK-selectable Brand Names for details.	Purpose unknown (set to "1")
        2	LK2	        Purpose unknown (set to "0")
        1	LK1	        Expansion Port /TEST pin
        0	CRTC VSYNC	Vertical Sync ("1"=VSYNC active, "0"=VSYNC inactive)	Same as on CPC

        LK1-4 are links on the mainboard ("0" bits are wired to GND). On CPC464,CPC664,CPC6128 and GX4000 they are labeled LK1-LK4, on the CPC464+ and CPC6128+ they are labeled LK101-LK103 
        (and LK104, presumably?).
        Bit5 (/EXP) can be used by a expansion device to report its presence. "1" = device connected, "0" = device not connected. 
        This is not always used by all expansion devices. is it used by any expansions? [in the DDI-1 disc interface, /EXP connects to the ROM bank selection, bank 0 or bank 7]
        If port B is programmed as an output, you can make a fake vsync visible to the Gate-Array by writing 1 to bit 0. You can then turn it off by writing 0 to bit 0. 
        It is fake in the sense that it is not generated by the CRTC as it normally is. This fake vsync doesn't work on all CPCs. It is not known if it is dependent on CRTC or 8255 or both.
        */

        /// <summary>
        /// Reads from Port B
        /// </summary>
        /// <returns></returns>
        private int INPortB()
        {
            if (PortB.Input)
            {
                // start with every bit reset
                BitArray rBits = new BitArray(8);

                // Bit0 - Vertical Sync ("1"=VSYNC active, "0"=VSYNC inactive)
                if (CRTC.VSYNC)
                    rBits[0] = true;

                // Bits1-3 - Distributor ID. Usually set to 4=Awa, 5=Schneider, or 7=Amstrad
                // force AMstrad
                rBits[1] = true;
                rBits[2] = true;
                rBits[3] = true;

                // Bit4 - Screen Refresh Rate ("1"=50Hz, "0"=60Hz)
                rBits[4] = true;

                // Bit5 - Expansion Port /EXP pin
                rBits[5] = false;

                // Bit6 - Parallel/Printer port ready signal, "1" = not ready, "0" = Ready
                rBits[6] = true;

                // Bit7 - Cassette data input
                rBits[7] = Tape.GetEarBit(_machine.CPU.TotalExecutedCycles);

                // return the byte
                byte[] bytes = new byte[1];
                rBits.CopyTo(bytes, 0);
                return bytes[0];
            }
            else
            {
                return PortB.Data;
            }
        }

        /// <summary>
        /// Writes to Port B
        /// </summary>
        private void OUTPortB(int data)
        {
            // just store the value
            PortB.Data = data;
        }

        #endregion

        #region Port C
        /*
        upper: I/O Mode 0, lower: I/O mode 0,
        upper: output, lower: output

        Bit	Description	Usage
        7	PSG BDIR	PSG function selection
        6	PSG BC1	
        5	Cassette    Write data	Cassette Out (sometimes also used as Printer Bit7, see 8bit Printer Ports)
        4	Cassette    Motor Control	set bit to "1" for motor on, or "0" for motor off
        0-3	Keyboard    line	Select keyboard line to be scanned (0-15)

        PSG function selection:

        Bit 7	Bit 6	Function
        0	    0	    Inactive
        0	    1	    Read from selected PSG register
        1	    0	    Write to selected PSG register
        1	    1	    Select PSG register
        */

        /// <summary>
        /// Reads from Port C
        /// </summary>
        /// <returns></returns>
        private int INPortC()
        {
            var val = PortCU.Data;

            if (PortCU.Input)
                val |= 0xf0;

            if (PortCL.Input)
                val |= 0x0f;

            return val;
        }

        /// <summary>
        /// Writes to Port C
        /// </summary>
        private void OUTPortC(int data)
        {
            PortCL.Data = data;
            PortCU.Data = data;

            if (!PortCU.Input)
            {
                // ay register set and write
                PSG.SelectedRegister = data;
                PSG.PortWrite(data);

                // cassette motor control
                byte val = (byte)data;
                var motor = val.Bit(4);
            }

            if (!PortCL.Input)
            {
                // which keyboard row to scan
                Keyboard.CurrentLine = PortCL.Data & 0x0f;
            }
        }

        #endregion

        #region PPI Control
        /*
        This register has two different functions depending on bit7 of the data written to this register.
        
        PPI Control with Bit7=1

        If Bit 7 is "1" then the other bits will initialize Port A-B as Input or Output:

        Bit 0    IO-Cl    Direction for Port C, lower bits (always 0=Output in CPC)
        Bit 1    IO-B     Direction for Port B             (always 1=Input in CPC)
        Bit 2    MS0      Mode for Port B and Port Cl      (always zero in CPC)
        Bit 3    IO-Ch    Direction for Port C, upper bits (always 0=Output in CPC)
        Bit 4    IO-A     Direction for Port A             (0=Output, 1=Input)
        Bit 5,6  MS0,MS1  Mode for Port A and Port Ch      (always zero in CPC)
        Bit 7    SF       Must be "1" to setup the above bits

        CAUTION: Writing to PIO Control Register (with Bit7 set), automatically resets PIO Ports A,B,C to 00h each!
        In the CPC only Bit 4 is of interest, all other bits are always having the same value. In order to write to the PSG sound registers, a value of 82h must 
        be written to this register. In order to read from the keyboard (through PSG register 0Eh), a value of 92h must be written to this register.

        PPI Control with Bit7=0

        Otherwise, if Bit 7 is "0" then the register is used to set or clear a single bit in Port C:

        Bit 0    B        New value for the specified bit (0=Clear, 1=Set)
        Bit 1-3  N0,N1,N2 Specifies the number of a bit (0-7) in Port C
        Bit 4-6  -        Not Used
        Bit 7    SF       Must be "0" in this case
        */

        /// <summary>
        /// Deals with bytes written to the control
        /// </summary>
        /// <param name="data"></param>
        private void ControlHandler(int data)
        {
            byte val = (byte)data;

            // Bit7 = 1
            if (val.Bit(7))
            {
                // Bit0 - Direction for Port C, lower bits
                PortCL.Input = val.Bit(0);

                // Bit1 - Direction for Port B
                PortB.Input = val.Bit(1);

                // Bit2 - Mode for Port B and Port Cl (CPC always 0)
                PortB.OpMode = 0;
                PortCL.OpMode = 0;

                // Bit3 - Direction for Port C, upper bits
                PortCU.Input = val.Bit(3);

                // Bit4 - Direction for Port A
                PortA.Input = val.Bit(4);

                // Bits 5,6 - Mode for Port A and Port Ch (CPC always 0)
                PortA.OpMode = 0;
                PortCU.OpMode = 0;

                // reset ports
                PortA.Data = 0x00;
                PortB.Data = 0x00;
                PortCL.Data = 0x00;
                PortCU.Data = 0x00;
            }
            // Bit7 = 0
            else
            {
                // Bit0 - New value for the specified bit (0=Clear, 1=Set)
                var newBit = val.Bit(0);

                // Bits 1-3 - Specifies the number of a bit (0-7) in Port C
                var bit = (data >> 1) & 7;

                if (newBit)
                {
                    // set the bit
                    PortCL.Data |= ~(1 << bit);
                    PortCU.Data |= ~(1 << bit);
                }
                else
                {
                    // reset the bit
                    PortCL.Data &= ~(1 << bit);
                    PortCU.Data &= ~(1 << bit);
                }

                if (!PortCL.Input)
                {
                    // keyboard set row
                }

                if (!PortCU.Input)
                {
                    // ay register set and write
                    PSG.SelectedRegister = val;
                    PSG.PortWrite(data);
                }
            }
        }

        #endregion

        #region IPortIODevice

        /// <summary>
        /// Device responds to an IN instruction
        /// </summary>
        /// <param name="port"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool ReadPort(ushort port, ref int result)
        {
            BitArray portBits = new BitArray(BitConverter.GetBytes(port));
            BitArray dataBits = new BitArray(BitConverter.GetBytes((byte)result));

            // The 8255 responds to bit 11 reset
            bool accessed = !portBits[11];
            if (!accessed)
                return false;

            if (!portBits[8] && !portBits[9])
            {
                // Port A Data
                // PSG (Sound/Keyboard/Joystick)
                result = INPortA();
            }

            if (portBits[8] && !portBits[9])
            {
                // Port B Data
                // Vsync/Jumpers/PrinterBusy/CasIn/Exp
                result = INPortB();
            }

            if (!portBits[8] && portBits[9])
            {
                // Port C Data
                // KeybRow/CasOut/PSG
                result = INPortC();
            }

            if (portBits[8] && portBits[9])
            {
                // Control
                return false;
            }

            return true;
        }

        /// <summary>
        /// Device responds to an OUT instruction
        /// </summary>
        /// <param name="port"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool WritePort(ushort port, int result)
        {
            BitArray portBits = new BitArray(BitConverter.GetBytes(port));
            BitArray dataBits = new BitArray(BitConverter.GetBytes((byte)result));

            // The 8255 responds to bit 11 reset
            bool accessed = !portBits[11];
            if (!accessed)
                return false;

            if (!portBits[8] && !portBits[9])
            {
                // Port A Data
                // PSG (Sound/Keyboard/Joystick)
                OUTPortA(result);
            }

            if (portBits[8] && !portBits[9])
            {
                // Port B Data
                // Vsync/Jumpers/PrinterBusy/CasIn/Exp
                OUTPortB(result);
            }

            if (!portBits[8] && portBits[9])
            {
                // Port C Data
                // KeybRow/CasOut/PSG
                OUTPortC(result);
            }

            if (portBits[8] && portBits[9])
            {
                // Control
                ControlHandler(result);
            }

            return true;
        }

        #endregion

        #region Serialization

        public void SyncState(Serializer ser)
        {
            ser.BeginSection("PPI");
            PortA.SyncState(ser);
            PortB.SyncState(ser);
            PortCU.SyncState(ser);
            PortCL.SyncState(ser);
            ser.EndSection();
        }

        #endregion
    }
}