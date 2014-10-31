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
     * This library implements basic functions to control a Dynamic Message Sign device via RS232 Serial port
     * The device must be able to accessed by Simple Network Management Protocol in application layer, under NTCIP 1203 standard 
     *      
     */
    public enum DataMode { Text, Hex }
    public class SerialDMS
    {

        private string _portName = "COM1";
        private string _community = "public";
        private string _owner = "DDOT ITS";
        private int _responseDelay = 800; // a delay is required to ensure the full stream is transmitted
        public delegate void logwriterFunction(string text);
        private logwriterFunction _logwriter = null;

        // Constructor, Parameter settings
        public SerialDMS(string portname, string community)
        {
            _portName = portname;
            _community = community;
        }

        public SerialDMS(string portname, string community, int responseDelay)
        {
            _portName = portname;
            _community = community;
            _responseDelay = responseDelay;
        }

        public SerialDMS(string portname, string community, logwriterFunction logwriter)
        {
            _portName = portname;
            _community = community;            
            _logwriter = logwriter;
        }

        public SerialDMS(string portname, string community, int responseDelay, logwriterFunction logwriter)
        {
            _portName = portname;
            _community = community;
            _responseDelay = responseDelay;
            _logwriter = logwriter;
        }

        // Get current message
        public string GetMessage()
        {
            try
            {
                SerialSNMP snmp = new SerialSNMP(_portName, _community, _responseDelay);
                snmp.Open();
                // Get the active message type.column
                KeyValuePair<Oid, AsnType> currentActiveLocationReponse = snmp.Get("1.3.6.1.4.1.1206.4.2.3.6.5.0");
                string[] m = currentActiveLocationReponse.Value.ToString().Split(" ".ToCharArray());
                int mtype = Convert.ToInt32(m[0], 16);
                //int mcol = Convert.ToInt32(m[0] + m[1], 16);
                int mcol = Convert.ToInt32(m[1] + m[2], 16);
                for (int i = 0; i < m.Length; i++)
                {
                    Console.WriteLine("{0:X}", m[i]);
                }
                if (_logwriter != null) _logwriter(mtype.ToString());
                if (_logwriter != null) _logwriter(mcol.ToString());

                // Get the current message
                KeyValuePair<Oid, AsnType> currentMessageReponse = snmp.Get(string.Format("1.3.6.1.4.1.1206.4.2.3.5.8.1.3.{0}.{1}", mtype, mcol));
                snmp.Close();
                return currentMessageReponse.Value.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return null;
            }
            
        }

        // Set message
        /// <summary>
        /// Switch the current display index. Use when CRC is unknown. Can be used for setting can messages. (.2.x)
        /// </summary>
        /// <param name="dmsip"></param>
        /// <param name="type"></param>
        /// <param name="column"></param>
        /// <returns></returns>
        public bool setDMS(int type, int column)
        {
            try
            {
                SerialSNMP snmp = new SerialSNMP(_portName, _community, _responseDelay);
                snmp.Open();
                //query for the CRC
                string oid = string.Format("1.3.6.1.4.1.1206.4.2.3.5.8.1.5.{0}.{1}", type.ToString(), column.ToString());
                Console.WriteLine(oid);
                KeyValuePair<Oid, AsnType> crcResult = snmp.Get(oid);
                Integer32 ICRC = (Integer32)crcResult.Value;
                int CRC = ICRC.Value;
                Console.WriteLine("set2");
                //switch the current display index
                snmp.Close();
                Console.WriteLine("set2 done");
                return setDMS(type, column, CRC);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// to set changeable message (.3.x, or .4.x)
        /// </summary>
        /// <param name="dmsip"></param>
        /// <param name="type"></param>
        /// <param name="column"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public bool setDMS(int type, int column, string msg)
        {
            try
            {
                SerialSNMP snmp = new SerialSNMP(_portName, _community,_responseDelay);
                snmp.Open();
                //send request of message modification
                string oid = string.Format(".1.3.6.1.4.1.1206.4.2.3.5.8.1.9.{0}.{1}", type.ToString(), column.ToString());
                if(_logwriter != null) _logwriter(oid);

                KeyValuePair<Oid, AsnType> result = snmp.Set(oid, new Integer32(6));

                //change the message
                oid = string.Format(".1.3.6.1.4.1.1206.4.2.3.5.8.1.3.{0}.{1}", type.ToString(), column.ToString());
                if (_logwriter != null) _logwriter(oid);
                result = snmp.Set(oid, new OctetString(msg));

                //set owner
                oid = string.Format(".1.3.6.1.4.1.1206.4.2.3.5.8.1.4.{0}.{1}", type.ToString(), column.ToString());
                if (_logwriter != null) _logwriter(oid);
                result = snmp.Set(oid, new OctetString(_owner)); 

                //set the priority
                oid = string.Format(".1.3.6.1.4.1.1206.4.2.3.5.8.1.8.{0}.{1}", type.ToString(), column.ToString());
                if (_logwriter != null) _logwriter(oid);
                result = snmp.Set(oid, new Integer32(1));

                //request for validation
                oid = string.Format(".1.3.6.1.4.1.1206.4.2.3.5.8.1.9.{0}.{1}", type.ToString(), column.ToString());
                if (_logwriter != null) _logwriter(oid);
                result = snmp.Set(oid, new Integer32(7));
                
                snmp.Close();
                
                //switch the current display index
                return setDMS(type, column);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                
                return false;
            }
        }

        /// <summary>
        ///MessageActivationCode (read-write)
        ///    .1.3.6.1.4.1.1206.4.2.3.6.3.0
        ///MessageActivationCode ::= OCTET STRING (SIZE(12))
        ///-- The MessageActivationCode consists of those parameters required to activate a
        ///-- message on a DMS.
        ///-- Duration 16 bits bit 0 to 15
        ///-- ActivatePriority 8 bits bit 16 to 23
        ///-- MsgMemoryType 8 bits bit 24 to 31
        ///-- MessageNumber 16 bits bit 32 to 47
        ///-- MessageCRC 16 bits bit 48 to 63
        ///-- SourceAddress 32 bits bit 64 to 95
        ///    FF FF FF 03 00 01 88 15 00 00 00 00  (code for 3.1)
        ///    FF FF FF 02 00 01 7A E2 00 00 00 00  (code for 2.1)
        ///             ^^ ^^^^^ ^^^^^
        ///            type column  CRC        
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="type"></param>
        /// <param name="column"></param>
        /// <param name="CRC"></param>
        /// <returns></returns>
        public bool setDMS(int type, int column, int CRC)
        {
            try
            {
                SerialSNMP snmp = new SerialSNMP(_portName, _community, _responseDelay);
                snmp.Open();
                string oid = ".1.3.6.1.4.1.1206.4.2.3.6.3.0";
                byte[] barray = new byte[12];
                for (int i = 0; i <= 2; i++) barray[i] = 255;
                barray[3] = (byte)type;
                barray[4] = (byte)((column >> 8) & 0x000000FF);
                barray[5] = (byte)(column & 0x000000FF);
                barray[6] = (byte)((CRC >> 8) & 0x000000FF);
                barray[7] = (byte)(CRC & 0x000000FF);
                for (int i = 8; i <= 11; i++) barray[i] = 0;

                OctetString os = new OctetString(barray);
                if (_logwriter != null) _logwriter(oid);
                KeyValuePair<Oid, AsnType> result = snmp.Set(oid, os);                
                snmp.Close();                
                if (result.Value == null) { return false; };
                return true;

            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return false;
            }
        }  

        // Construct SNMP PDU

    }
}
