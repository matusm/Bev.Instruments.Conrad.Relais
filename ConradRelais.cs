using System;
using System.IO.Ports;
using System.Threading;

namespace Bev.Instruments.Conrad.Relais
{
    public class ConradRelais
    {
        private readonly SerialPort comPort;
        private const int COM_DELAY = 100;  // in ms, must be found experimentally

        private byte fwVersion;     // firmware version of first board
        private byte firstAddress;  // address of first board
        private byte tempData;      // data returned of board: bAddr
        private int numberBoards;   // number of boards detected by Setup

        public ConradRelais(string portName)
        {
            DevicePort = portName.Trim();
            comPort = new SerialPort(DevicePort, 19200, Parity.None, 8, StopBits.One);
            comPort.Handshake = Handshake.None;
            comPort.Open();
            comPort.ReadTimeout = 3000;
            comPort.WriteTimeout = 3000;
            Setup(1);
            Thread.Sleep(COM_DELAY);
        }

        public string DevicePort { get; }
        public string InstrumentManufacturer => "Conrad Electronic SE";
        public string InstrumentType => "197720";
        public string InstrumentFirmwareVersion => fwVersion.ToString();
        public int NumberOfBoards => numberBoards;

        // switch all 8 relays according to bData
        public void SetRelays(byte address, byte data) => ControlBoard(3, address, data);
        public void SetRelays(byte data) => SetRelays(firstAddress, data);

        // query status of all 8 relays, result is in bData
        public void GetRelays(byte address) => ControlBoard(2, address, 0);
        public void GetRelays() => GetRelays(firstAddress);

        // activate individual relays
        public void SingleOn(byte address, byte data) => ControlBoard(6, address, data);
        public void SingleOn(byte data) => SingleOn(firstAddress, data);

        // deactivate individual relays
        public void SingleOff(byte address, byte data) => ControlBoard(7, address, data);
        public void SingleOff(byte data) => SingleOff(firstAddress, data);

        // toggle individual relays
        public void SingleToggle(byte address, byte data) => ControlBoard(8, address, data);
        public void SingleToggle(byte data) => SingleToggle(firstAddress, data);

        public void On(int channel) => SingleOn(Mask(channel));

        public void Off(int channel) => SingleOff(Mask(channel));

        private byte Mask(int channel)
        {
            switch (channel)
            {
                case 1:
                    return 1;
                case 2:
                    return 2;
                case 3:
                    return 4;
                case 4:
                    return 8;
                case 5:
                    return 16;
                case 6:
                    return 32;
                case 7:
                    return 64;
                case 8:
                    return 128;
                default:
                    return 0;
            }
        }

        private void Setup(byte address)
        {
            bool flag = ControlBoard(1, address, 0);
            if (flag)
                fwVersion = tempData;
        }

		private bool ControlBoard(byte command, byte address, byte data)
		{
            byte[] frame = BuildUpFrame(command, address, data);
			// Console.Write("\n***** frame:");
			// foreach (byte b in frame) Console.Write(" {0,2:X2}", b);
			if (!SendToBoard(frame))
				return false; // something went wrong while sending the frame
			Thread.Sleep(COM_DELAY);
            byte[] response = ReadFromBoard();
			// Console.Write(" ***** response:");
			// foreach (byte b in response) Console.Write(" {0,2:X2}", b);
			// here we analyze the data from the first board only!
			if (response.Length < 4)
				return false;
			if (response.Length % 4 != 0)
				return false;
			// check XOR check sum
			byte xor = (byte)(response[0] ^ response[1] ^ response[2]);
			if (xor != response[3])
				return false;
			// check if response fits to command 
			if ((response[0] + frame[0]) != 255)
				return false;
			firstAddress = response[1];
			tempData = response[2];
			if (frame[0] == 1)
				numberBoards = response.Length / 4 - 1;
			return true;
		}

        private byte[] BuildUpFrame(byte command, byte address, byte data)
        {
            byte[] buffer = new byte[4];
            buffer[0] = command;
            buffer[1] = address;
            buffer[2] = data;
            buffer[3] = (byte)(buffer[0] ^ buffer[1] ^ buffer[2]); // [XOR check sum]
            return buffer;
        }

        private bool SendToBoard(byte[] frame)
        {
            try
            {
                comPort.Write(frame, 0, frame.Length);
                return true;
            }
            catch (Exception e)
            {
                // Console.WriteLine("SendToBoard failed: ", e);
                return false;
            }
        }

        private byte[] ReadFromBoard()
        {
            byte[] buffer = new byte[comPort.BytesToRead];
            try
            {
                comPort.Read(buffer, 0, buffer.Length);
                return buffer;
            }
            catch (Exception e)
            {
                // Console.WriteLine ("ReadFromBoard failed: ", e);
                return buffer;
            }
        }

    }
}
