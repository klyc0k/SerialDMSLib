using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.IO;
using SnmpSharpNet;

namespace SerialDMSlib
{
    /* 
    * Author: Kevin Lu (kevinlu at vt.edu)
    * Created: Dec. 2013
    * 
    * This code implemented transmitting and receiving SNMP packets via serial port
    *      
    */
    public class SerialSNMP
    {
        enum SendRequestType {GET, SET}
        private SerialPort _comport;

        public int ResponseDelay = 1000;
        public string Community = "public";
        public int BaudRate = 9600;
        public int DataBits = 8;
        public StopBits StopBits = StopBits.One;
        public string PortName = "COM1";
        public int WriteTimeout = 8000;
        public int ReadTimeout = 5000;

        public SerialSNMP()
        {
        }

        public SerialSNMP(string portName, string community, int responseDelay)
        {
            ResponseDelay = responseDelay;
            Community = community;
            PortName = portName;
        }

        /// <summary>
        /// Open a connection to the target device
        /// </summary>
        public void Open()
        {
            _comport = new SerialPort();
            _comport.BaudRate = BaudRate;
            _comport.PortName = PortName;
            _comport.DataBits = DataBits;
            _comport.StopBits = StopBits;
            _comport.WriteTimeout = WriteTimeout;
            _comport.ReadTimeout = ReadTimeout;
            _comport.Open();
        }

        public void Close()
        {
            _comport.Close();
            _comport.Dispose();
        }

        /// <summary>
        /// SNMP Get -- Get the value by OID
        /// </summary>
        /// <param name="oid">  </param>
        /// <returns> KeyValuePairs </returns>
        public KeyValuePair<Oid, AsnType> Get(string oid)
        {
            VbCollection vbc = new VbCollection();
            vbc.Add(oid);
         
            Pdu pdu = Pdu.GetPdu(vbc);
            pdu.RequestId = 0;
            SnmpV1Packet packet = Wrapup(Community, pdu, SendRequestType.GET);
           
            Transmit(packet);

            Thread.Sleep(ResponseDelay);

            byte[] result = Receive();
            byte[] exresult = extract_SNMP(result);

            SnmpV1Packet v1 = new SnmpV1Packet();
            v1.decode(exresult, exresult.Length);

            KeyValuePair<Oid, AsnType> kvp = new KeyValuePair<Oid, AsnType>(v1.Pdu.VbList[oid].Oid, v1.Pdu.VbList[oid].Value);

            return kvp;
        }

        /// <summary>
        /// SNMP Set -- Set values
        /// </summary>
        /// <param name="pdu"></param>
        /// <returns></returns>
        public KeyValuePair<Oid, AsnType> Set(string oid, AsnType value)
        {
            VbCollection vbc = new VbCollection();
            Vb v = new Vb(oid);
            v.Value = value;
            vbc.Add(v);
            Pdu pdu = Pdu.SetPdu(vbc);

            SnmpV1Packet packet = Wrapup(Community, pdu, SendRequestType.SET);
            Transmit(packet);

            Thread.Sleep(ResponseDelay);

            byte[] result = Receive();
            byte[] exresult = extract_SNMP(result);

            SnmpV1Packet v1 = new SnmpV1Packet();
            v1.decode(exresult, exresult.Length);
            KeyValuePair<Oid, AsnType> kvp = new KeyValuePair<Oid, AsnType>(v1.Pdu.VbList[oid].Oid, v1.Pdu.VbList[oid].Value);

            return kvp;
        }        

        private SnmpV1Packet Wrapup(string community, Pdu pdu, SendRequestType reqtype){
            SnmpV1Packet v1 = new SnmpV1Packet(community);
            v1._pdu = pdu;
            return v1;            
        }

        /// <summary>
        /// Transmit the SNMP packets
        /// </summary>
        /// <param name="snmppacket"></param>
        protected void Transmit(SnmpV1Packet snmppacket){
            byte [] s = snmppacket.encode();
            byte [] transbytes = new byte[s.Length + 7]; // head 4 and tail 3
            
            // The first byte is flag
            transbytes[0] = 0x7e;

            // The next three bytes are address (hardcoded here)
            transbytes[1] = 0x05;
            transbytes[2] = 0x13;
            transbytes[3] = 0xc1;

            // SNMP packet
            for (int i = 0; i<s.Length; i++){
                transbytes[i+4] = s[i];
            }

            // Calculate two CRC bytes 
            byte[] data = new byte[s.Length+3];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = transbytes[i + 1];
            }
            byte [] crcbytes = CRC.compute_transmission_crc(data);

            for (int i = 0; i < 2; i++)
            {
                transbytes[i + s.Length + 4] = crcbytes[i];
            }
            
            // The last byte is the flag
            transbytes[transbytes.Length - 1] = 0x7e;

            _comport.Write(transbytes, 0, transbytes.Length);
        }

        /// <summary>
        /// Receive stream from serial port and recognize as a packet
        /// </summary>
        /// <returns></returns>
        protected byte[] Receive()
        {
            if (!_comport.IsOpen) return null;

            List<byte> sb = new List<byte>();
            _comport.Encoding = Encoding.ASCII;
                   
            while (_comport.BytesToRead > 0)
            {
                byte bt = (byte)_comport.ReadByte();
                sb.Add(bt);                
                Thread.Sleep(50);                
            }
            return sb.ToArray();
        }

        /// <summary>
        /// Process the data stream and identify the SNMP packet
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private byte[] extract_SNMP(byte [] data)
        {            
            byte [] snmpdata = new byte[data.Length - 7];
            for (int i = 0; i < snmpdata.Length; i++)
            {
                snmpdata[i] = data[i + 4];
            }
            return snmpdata;
        }

        private byte[] HexStringToByteArray(string s)
        {
            s = s.Replace(" ", "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
            return buffer;
        }
        
    }
}
