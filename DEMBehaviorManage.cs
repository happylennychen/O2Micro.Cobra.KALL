//#define debug
//#if debug
//#define functiontimeout
//#define pec
//#define frozen
//#define dirty
//#define readback
//#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.ComponentModel;
using Cobra.Communication;
using Cobra.Common;
using System.IO;

namespace Cobra.KALL
{
    internal class DEMBehaviorManage
    {
        private byte calATECRC;
        private byte calUSRCRC;
        //父对象保存
        private DEMDeviceManage m_parent;
        public DEMDeviceManage parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        UInt16[] EFUSEUSRbuf = new UInt16[ElementDefine.EF_USR_TOP - ElementDefine.EF_USR_OFFSET + 1];      //Used for read back check

        private object m_lock = new object();
        private CCommunicateManager m_Interface = new CCommunicateManager();

        public void Init(object pParent)
        {
            parent = (DEMDeviceManage)pParent;
            CreateInterface();

        }

        #region 端口操作
        public bool CreateInterface()
        {
            bool bdevice = EnumerateInterface();
            if (!bdevice) return false;

            return m_Interface.OpenDevice(ref parent.m_busoption);
        }

        public bool DestroyInterface()
        {
            return m_Interface.CloseDevice();
        }

        public bool EnumerateInterface()
        {
            return m_Interface.FindDevices(ref parent.m_busoption);
        }
        #endregion

        #region 操作寄存器操作
        #region 操作寄存器父级操作
        protected UInt32 ReadByte(byte reg, ref byte pval)
        {
            UInt32 ret = 0;
            lock (m_lock)
            {
                ret = OnReadByte(reg, ref pval);
            }
            return ret;
        }

        protected UInt32 WriteByte(byte reg, byte val)
        {
            UInt32 ret = 0;
            lock (m_lock)
            {
                ret = OnWriteByte(reg, val);
            }
            return ret;
        }


        protected UInt32 SetWorkMode(ElementDefine.WORK_MODE wkm)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            lock (m_lock)
            {
                ret = OnSetWorkMode(wkm);
            }
            return ret;
        }


        protected UInt32 GetWorkMode(ref ElementDefine.WORK_MODE wkm)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            lock (m_lock)
            {
                ret = OnGetWorkMode(ref wkm);
            }
            return ret;
        }

        protected UInt32 PowerOn()
        {
            UInt32 ret = 0;
            lock (m_lock)
            {
                ret = OnPowerOn();
            }
            return ret;
        }
        protected UInt32 PowerOff()
        {
            UInt32 ret = 0;
            lock (m_lock)
            {
                ret = OnPowerOff();
            }
            return ret;
        }

        #endregion

        #region 操作寄存器子级操作
        protected byte crc8_calc(ref byte[] pdata, UInt16 n)
        {
            byte crc = 0;
            byte crcdata;
            UInt16 i, j;

            for (i = 0; i < n; i++)
            {
                crcdata = pdata[i];
                for (j = 0x80; j != 0; j >>= 1)
                {
                    if ((crc & 0x80) != 0)
                    {
                        crc <<= 1;
                        crc ^= 0x07;
                    }
                    else
                        crc <<= 1;

                    if ((crcdata & j) != 0)
                        crc ^= 0x07;
                }
            }
            return crc;
        }

        protected byte calc_crc_read(byte slave_addr, byte reg_addr, byte data)
        {
            byte[] pdata = new byte[5];

            pdata[0] = slave_addr;
            pdata[1] = reg_addr;
            pdata[2] = (byte)(slave_addr | 0x01);
            pdata[3] = data;

            return crc8_calc(ref pdata, 4);
        }

        protected byte calc_crc_write(byte slave_addr, byte reg_addr, byte data)
        {
            byte[] pdata = new byte[4];

            pdata[0] = slave_addr; ;
            pdata[1] = reg_addr;
            pdata[2] = data;

            return crc8_calc(ref pdata, 3);
        }

        protected UInt32 OnReadByte(byte reg, ref byte pval)
        {
            UInt16 DataOutLen = 0;
            byte[] sendbuf = new byte[2];
            byte[] receivebuf = new byte[2];
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            try
            {
                sendbuf[0] = (byte)parent.m_busoption.GetOptionsByGuid(BusOptions.I2CAddress_GUID).SelectLocation.Code;
            }
            catch (System.Exception ex)
            {
                return ret = LibErrorCode.IDS_ERR_DEM_LOST_PARAMETER;
            }
            sendbuf[1] = reg;

            for (int i = 0; i < ElementDefine.RETRY_COUNTER; i++)
            {
                if (m_Interface.ReadDevice(sendbuf, ref receivebuf, ref DataOutLen, 2))
                {
                    if (receivebuf[1] != calc_crc_read(sendbuf[0], sendbuf[1], receivebuf[0]))
                    {
                        return LibErrorCode.IDS_ERR_BUS_DATA_PEC_ERROR;
                    }
                    pval = receivebuf[0];
                    break;
                }
                Thread.Sleep(10);
            }

            m_Interface.GetLastErrorCode(ref ret);
            return ret;
        }

        protected UInt32 OnWriteByte(byte reg, byte val)
        {
            UInt16 DataOutLen = 0;
            byte[] sendbuf = new byte[4];
            byte[] receivebuf = new byte[1];
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            try
            {
                sendbuf[0] = (byte)parent.m_busoption.GetOptionsByGuid(BusOptions.I2CAddress_GUID).SelectLocation.Code;
            }
            catch (System.Exception ex)
            {
                return ret = LibErrorCode.IDS_ERR_DEM_LOST_PARAMETER;
            }
            sendbuf[1] = reg;
            sendbuf[2] = val;

            sendbuf[3] = calc_crc_write(sendbuf[0], sendbuf[1], sendbuf[2]);
            for (int i = 0; i < ElementDefine.RETRY_COUNTER; i++)
            {
                if (m_Interface.WriteDevice(sendbuf, ref receivebuf, ref DataOutLen, 2))
                    break;
                Thread.Sleep(10);
            }

            m_Interface.GetLastErrorCode(ref ret);
            return ret;
        }

        protected UInt32 OnGetWorkMode(ref ElementDefine.WORK_MODE wkm)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            byte buf = 0;
            ret = OnReadByte(ElementDefine.WORKMODE_OFFSET, ref buf);
            buf &= 0x03;
            wkm = (ElementDefine.WORK_MODE)buf;
            return ret;
        }

        protected UInt32 OnSetWorkMode(ElementDefine.WORK_MODE wkm)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = OnWriteByte(ElementDefine.WORKMODE_OFFSET, (byte)wkm);
            return ret;
        }

        private UInt32 OnPowerOn()
        {
            byte[] yDataIn = { 0x51 };
            byte[] yDataOut = { 0, 0 };
            ushort uOutLength = 2;
            ushort uWrite = 1;
            if (m_Interface.SendCommandtoAdapter(yDataIn, ref yDataOut, ref uOutLength, uWrite))
            {
                if (uOutLength == 2 && yDataOut[0] == 0x51 && yDataOut[1] == 0x1)
                {
                    Thread.Sleep(200);
                    return LibErrorCode.IDS_ERR_SUCCESSFUL;
                }
                else
                    return ElementDefine.IDS_ERR_DEM_POWERON_FAILED;
            }
            return ElementDefine.IDS_ERR_DEM_POWERON_FAILED;
        }

        private UInt32 OnPowerOff()
        {
            byte[] yDataIn = { 0x52 };
            byte[] yDataOut = { 0, 0 };
            ushort uOutLength = 2;
            ushort uWrite = 1;
            if (m_Interface.SendCommandtoAdapter(yDataIn, ref yDataOut, ref uOutLength, uWrite))
            {
                if (uOutLength == 2 && yDataOut[0] == 0x52 && yDataOut[1] == 0x2)
                {
                    return LibErrorCode.IDS_ERR_SUCCESSFUL;
                }
                else
                    return ElementDefine.IDS_ERR_DEM_POWEROFF_FAILED;
            }
            return ElementDefine.IDS_ERR_DEM_POWEROFF_FAILED;
        }

        #endregion
        #endregion

        #region 基础服务功能设计

        public UInt32 Read(ref TASKMessage msg)
        {
            Reg reg = null;
            bool bsim = true;
            byte baddress = 0;
            byte bdata = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<byte> EFUSEReglist = new List<byte>();
            List<byte> OpReglist = new List<byte>();

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            AutomationElement aElem = parent.m_busoption.GetATMElementbyGuid(AutomationElement.GUIDATMTestStart);
            if (aElem != null)
            {
                bsim |= (aElem.dbValue > 0.0) ? true : false;
                aElem = parent.m_busoption.GetATMElementbyGuid(AutomationElement.GUIDATMTestSimulation);
                bsim |= (aElem.dbValue > 0.0) ? true : false;
            }

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                switch (p.guid & ElementDefine.SectionMask)
                {
                    case ElementDefine.EFUSEElement:
                        {
                            if (p == null) break;
                            if (p.errorcode == LibErrorCode.IDS_ERR_DEM_PARAM_READ_WRITE_UNABLE) continue;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;
                                EFUSEReglist.Add(baddress);
                            }
                            break;
                        }
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;
                                OpReglist.Add(baddress);
                            }
                            break;
                        }
                        //case ElementDefine.TemperatureElement:
                        //break;
                }
            }

            EFUSEReglist = EFUSEReglist.Distinct().ToList();
            OpReglist = OpReglist.Distinct().ToList();
            //Read 
            if (EFUSEReglist.Count != 0)
            {
                List<byte> EFATEList = new List<byte>();
                List<byte> EFUSRList = new List<byte>();
                foreach (byte addr in EFUSEReglist)
                {
                    if (addr <= ElementDefine.EF_ATE_TOP && addr >= ElementDefine.EF_MEMORY_OFFSET)
                        EFATEList.Add(addr);
                    else if (addr <= ElementDefine.EF_USR_TOP && addr >= ElementDefine.EF_USR_OFFSET)
                        EFUSRList.Add(addr);
                }
                if (EFATEList.Count != 0)
                {
                    ret = CheckATECRC();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                }

                if (EFUSRList.Count != 0)
                {
                    ret = CheckUSRCRC();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                }
            }
            foreach (byte badd in OpReglist)
            {
                ret = ReadByte(badd, ref bdata);
                parent.m_OpRegImg[badd].err = ret;
                parent.m_OpRegImg[badd].val = bdata;
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
            }
            return ret;
        }

        private bool isATEFRZ()
        {
            return (parent.m_EFRegImgEX[ElementDefine.EF_ATE_TOP - ElementDefine.EF_MEMORY_OFFSET].val & 0x0001) == 0x0001;
        }

        private bool isUSRFRZ()
        {
            return (parent.m_EFRegImgEX[ElementDefine.EF_USR_TOP - ElementDefine.EF_MEMORY_OFFSET].val & 0x0001) == 0x0001;
        }
#if true
        #region Efuse_ATE
        private UInt32 CheckATECRC()
        {
            //UInt16 len = 8;
            //byte tmp = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            byte[] atebuf = new byte[ElementDefine.ATE_CRC_BUF_LEN];

            ret = ReadATECRCRefReg();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            if (!isATEFRZ())
                return LibErrorCode.IDS_ERR_SUCCESSFUL;

            GetATECRCRef(ref atebuf);
            calATECRC = CalEFUSECRC(atebuf, ElementDefine.ATE_CRC_BUF_LEN);

            byte readATECRC = 0;
            ret = ReadATECRC(ref readATECRC);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            if (readATECRC == calATECRC)
                return LibErrorCode.IDS_ERR_SUCCESSFUL;
            else
            {
                parent.m_EFRegImgEX[ElementDefine.EF_ATE_TOP - ElementDefine.EF_MEMORY_OFFSET].err = LibErrorCode.IDS_ERR_DEM_ATE_CRC_ERROR;
                return LibErrorCode.IDS_ERR_DEM_ATE_CRC_ERROR;
            }
        }

        private UInt32 ReadATECRCRefReg()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            for (byte i = (byte)ElementDefine.EF_ATE_OFFSET; i <= (byte)ElementDefine.EF_ATE_TOP; i++)
            {
                byte bdata = 0;
                parent.m_EFRegImg[i].err = ReadByte(i, ref bdata);
                parent.m_EFRegImg[i].val = bdata;
                ret |= parent.m_EFRegImg[i].err;
            }
            return ret;
        }
        private void GetATECRCRef(ref byte[] buf)
        {
            for (byte i = 0; i < 24; i++)
            {
                byte shiftdigit = (byte)((i % 4) * 4);
                shiftdigit = (byte)(12 - shiftdigit);
                int reg = i / 4;
                buf[i] = (byte)((parent.m_EFRegImgEX[reg].val & (0x0f << shiftdigit)) >> shiftdigit);
            }
            buf[24] = (byte)((parent.m_EFRegImgEX[6].val & (0x0f << 12)) >> 12);
            buf[25] = (byte)((parent.m_EFRegImgEX[6].val & (0x0f << 8)) >> 8);
            buf[26] = (byte)((parent.m_EFRegImgEX[6].val & (0x0f << 4)) >> 4);
        }
        private UInt32 ReadATECRC(ref byte crc)
        {
            byte bdata = 0;
            parent.m_EFRegImg[ElementDefine.ATE_CRC_OFFSET].err = ReadByte((byte)ElementDefine.ATE_CRC_OFFSET, ref bdata);
            if (parent.m_EFRegImg[ElementDefine.ATE_CRC_OFFSET].err != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return parent.m_EFRegImg[ElementDefine.ATE_CRC_OFFSET].err;
            parent.m_EFRegImg[ElementDefine.ATE_CRC_OFFSET].val = bdata;
            crc = (byte)(bdata & 0x000f);
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        #endregion
#endif
        #region Efuse_USR
        private UInt32 CheckUSRCRC()
        {
            //UInt16 len = 8;
            //byte tmp = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = ReadUSRCRCRefReg();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            if (!isUSRFRZ())
                return LibErrorCode.IDS_ERR_SUCCESSFUL;

            byte[] usrbuf = new byte[ElementDefine.USR_CRC_BUF_LEN];
            GetUSRCRCRef(ref usrbuf);
            calUSRCRC = CalEFUSECRC(usrbuf, ElementDefine.USR_CRC_BUF_LEN);
            byte readUSRCRC = 0;
            ret = ReadUSRCRC(ref readUSRCRC);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            if (calUSRCRC == readUSRCRC)
                return LibErrorCode.IDS_ERR_SUCCESSFUL;
            else
            {
                parent.m_EFRegImgEX[0x0f].err = LibErrorCode.IDS_ERR_DEM_ATE_CRC_ERROR;
                return LibErrorCode.IDS_ERR_DEM_ATE_CRC_ERROR;
            }
        }




        private UInt32 ReadUSRCRCRefReg()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            for (byte i = (byte)ElementDefine.EF_USR_OFFSET; i <= ElementDefine.EF_USR_TOP; i++)
            {
                byte bdata = 0;
                parent.m_EFRegImg[i].err = ReadByte(i, ref bdata);
                parent.m_EFRegImg[i].val = bdata;
                ret |= parent.m_EFRegImg[i].err;
            }
            return ret;
        }
        private void GetUSRCRCRef(ref byte[] buf)
        {
            for (byte i = 0; i < ElementDefine.USR_CRC_BUF_LEN; i++)
            {
                int reg = i + ElementDefine.EF_USR_OFFSET;
                buf[i] = (byte)(parent.m_EFRegImg[reg].val);
            }
        }
        private UInt32 ReadUSRCRC(ref byte crc)
        {
            byte bdata = 0;
            parent.m_EFRegImg[ElementDefine.USR_CRC_OFFSET].err = ReadByte((byte)ElementDefine.USR_CRC_OFFSET, ref bdata);
            if (parent.m_EFRegImg[ElementDefine.USR_CRC_OFFSET].err != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return parent.m_EFRegImg[ElementDefine.USR_CRC_OFFSET].err;
            parent.m_EFRegImg[ElementDefine.USR_CRC_OFFSET].val = bdata;
            crc = bdata;
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        #endregion

        private byte CalEFUSECRC(byte[] buf, UInt16 len)
        {
            return crc8_calc(ref buf, len);
        }

        /*
        private byte crc4_calc(byte[] pdata, int len)
        {

            byte crc = 0;
            byte crcdata;
            //byte poly = 0x07;             // poly
            //uint p = (uint)poly + 0x100;
            int n, j;                                      // the length of the data


            for (n = len - 1; n >= 0; n--)
            {
                crcdata = pdata[n];
                for (j = 0x8; j > 0; j >>= 1)
                {
                    if ((crc & 0x8) != 0)
                    {
                        crc <<= 1;
                        crc ^= 0x3;
                    }
                    else
                        crc <<= 1;
                    if ((crcdata & j) != 0)
                        crc ^= 0x3;
                }
                crc = (byte)(crc & 0xf);
            }

            return crc;
        }
        */

        private byte crc4_calc(byte[] pdata, int len)
        {

            byte crc = 0;
            byte crcdata;
            byte poly = 0x03;             // poly
            int n, j;                                      // the length of the data

            for (n = 0; n < len; n++)
            {
                crcdata = pdata[n];
                for (j = 0x8; j > 0; j >>= 1)
                {
                    if ((crc & 0x8) != 0)
                    {
                        crc <<= 1;
                        crc ^= poly;
                    }
                    else
                        crc <<= 1;
                    if ((crcdata & j) != 0)
                        crc ^= poly;
                }
                crc = (byte)(crc & 0xf);
            }
            return crc;
        }

        public UInt32 Write(ref TASKMessage msg)
        {
            Reg reg = null;
            byte baddress = 0;
            byte pval = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            UInt32 ret1 = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<byte> EFUSEReglist = new List<byte>();
            List<byte> EFUSEATEReglist = new List<byte>();
            UInt16[] EFUSEATEbuf = new UInt16[8];
            List<byte> EFUSEUSRReglist = new List<byte>();
            UInt16[] EFUSEUSRbuf = new UInt16[8];
            List<byte> OpReglist = new List<byte>();
            UInt16[] pdata = new UInt16[6];

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                switch (p.guid & ElementDefine.SectionMask)
                {
                    case ElementDefine.EFUSEElement:
                        {
                            if (p == null) break;
                            if ((p.errorcode == LibErrorCode.IDS_ERR_DEM_PARAM_READ_WRITE_UNABLE) || (p.errorcode == LibErrorCode.IDS_ERR_DEM_PARAM_WRITE_UNABLE)) continue;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;
                                EFUSEReglist.Add(baddress);
                            }
                            break;
                        }
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;
                                OpReglist.Add(baddress);
                            }
                            break;
                        }
                        //case ElementDefine.TemperatureElement:
                        //break;
                }
            }

            EFUSEReglist = EFUSEReglist.Distinct().ToList();
            OpReglist = OpReglist.Distinct().ToList();

            //Write 
            if (EFUSEReglist.Count != 0)
            {

                msg.gm.message = "Efuse can only be written once! Continue?";
                msg.controlreq = COMMON_CONTROL.COMMON_CONTROL_SELECT;
                if (!msg.controlmsg.bcancel) return LibErrorCode.IDS_ERR_DEM_USER_QUIT;

                foreach (byte addr in EFUSEReglist)
                {
                    if (addr <= ElementDefine.EF_ATE_TOP)
                        EFUSEATEReglist.Add(addr);
                    else
                        EFUSEUSRReglist.Add(addr);
                }

                if (EFUSEATEReglist.Count > 0)  //Y版本
                {
                    /*OnReadByte((byte)ElementDefine.EF_ATE_TOP, ref pval);
                    if ((pval & 0x0001) == 0x0001)
                    {
                        return LibErrorCode.IDS_ERR_DEM_FROZEN;
                    }
                    parent.m_EFRegImg[ElementDefine.EF_ATE_TOP].val |= 0x0001;*/    //Set Frozen bit in image
                }

                OnReadByte((byte)ElementDefine.EF_USR_TOP, ref pval);
                if ((pval & 0x0001) == 0x0001)
                {
                    return LibErrorCode.IDS_ERR_DEM_FROZEN;
                }
                parent.m_EFRegImg[ElementDefine.EF_USR_TOP].val |= 0x0001;    //Set Frozen bit in image

                msg.gm.message = "Please change to program voltage, then continue!";
                msg.controlreq = COMMON_CONTROL.COMMON_CONTROL_SELECT;
                if (!msg.controlmsg.bcancel) return LibErrorCode.IDS_ERR_DEM_USER_QUIT;

                //while (IsEfuseBusy()) ;

                SetWorkMode(ElementDefine.WORK_MODE.PROGRAM);


                if (EFUSEATEReglist.Count > 0)  //Y版本
                {
                    /*byte[] atebuf = new byte[ElementDefine.ATE_CRC_BUF_LEN];
                    GetATECRCRef(ref atebuf);
                    parent.m_EFRegImg[ElementDefine.EF_ATE_TOP].val &= 0xfff0;
                    parent.m_EFRegImg[ElementDefine.EF_ATE_TOP].val |= CalEFUSECRC(atebuf, ElementDefine.ATE_CRC_BUF_LEN);

                    foreach (byte badd in EFUSEATEReglist)
                    {
                        ret1 = parent.m_EFRegImg[badd].err;
                        ret |= ret1;
                        if (ret1 != LibErrorCode.IDS_ERR_SUCCESSFUL) continue;

                        EFUSEATEbuf[badd - ElementDefine.EF_ATE_OFFSET] = parent.m_EFRegImg[badd].val;
                        ret1 = OnWriteByte(badd, (byte)parent.m_EFRegImg[badd].val);
                        parent.m_EFRegImg[badd].err = ret1;
                        ret |= ret1;
                    }*/
                }

                byte[] usrbuf = new byte[ElementDefine.USR_CRC_BUF_LEN];
                GetUSRCRCRef(ref usrbuf);
                parent.m_EFRegImg[ElementDefine.USR_CRC_OFFSET].val = CalEFUSECRC(usrbuf, ElementDefine.USR_CRC_BUF_LEN);

                for (byte badd = (byte)ElementDefine.EF_USR_OFFSET; badd <= ElementDefine.EF_USR_TOP; badd++)
                {
                    ret1 = parent.m_EFRegImg[badd].err;
                    ret |= ret1;
                    if (ret1 != LibErrorCode.IDS_ERR_SUCCESSFUL) continue;

                    EFUSEUSRbuf[badd - ElementDefine.EF_USR_OFFSET] = parent.m_EFRegImg[badd].val;
                    ret1 = OnWriteByte(badd, (byte)parent.m_EFRegImg[badd].val);
                    parent.m_EFRegImg[badd].err = ret1;
                    ret |= ret1;
                }

                msg.gm.message = "Please change to normal voltage, then continue!";
                msg.controlreq = COMMON_CONTROL.COMMON_CONTROL_SELECT;
                if (!msg.controlmsg.bcancel) return LibErrorCode.IDS_ERR_DEM_USER_QUIT;

                foreach (byte badd in EFUSEATEReglist)
                {
                    //EFUSEATEbuf[badd - ElementDefine.EF_MEMORY_OFFSET] = (byte)parent.m_EFRegImg[badd - ElementDefine.EF_MEMORY_OFFSET].val;
                    ret1 = OnReadByte(badd, ref pval);
                    if (pval != EFUSEATEbuf[badd - ElementDefine.EF_ATE_OFFSET])
                        return LibErrorCode.IDS_ERR_DEM_BUF_CHECK_FAIL;
                }

                for (byte badd = (byte)ElementDefine.EF_USR_OFFSET; badd <= ElementDefine.EF_USR_TOP; badd++)
                {
                    //EFUSEATEbuf[badd - ElementDefine.EF_MEMORY_OFFSET] = (byte)parent.m_EFRegImg[badd - ElementDefine.EF_MEMORY_OFFSET].val;
                    ret1 = OnReadByte(badd, ref pval);
                    if (pval != EFUSEUSRbuf[badd - ElementDefine.EF_USR_OFFSET])
                        return LibErrorCode.IDS_ERR_DEM_BUF_CHECK_FAIL;
                }

                SetWorkMode(ElementDefine.WORK_MODE.NORMAL);

            }

            if (msg.gm.sflname == "Register Config")
            {
                SetWorkMode(ElementDefine.WORK_MODE.INTERNAL);
            }
            foreach (byte badd in OpReglist)
            {
                ret = WriteByte(badd, (byte)parent.m_OpRegImg[badd].val);
                parent.m_OpRegImg[badd].err = ret;
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
            }

            return ret;
        }

        public UInt32 BitOperation(ref TASKMessage msg)
        {
            Reg reg = null;
            byte baddress = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<byte> OpReglist = new List<byte>();

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                switch (p.guid & ElementDefine.SectionMask)
                {
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;

                                parent.m_OpRegImg[baddress].val = 0x00;
                                parent.WriteToRegImg(p, 1);
                                OpReglist.Add(baddress);

                            }
                            break;
                        }
                }
            }

            OpReglist = OpReglist.Distinct().ToList();

            //Write 
            foreach (byte badd in OpReglist)
            {
                ret = WriteByte(badd, (byte)parent.m_OpRegImg[badd].val);
                parent.m_OpRegImg[badd].err = ret;
            }

            return ret;
        }

        public UInt32 ConvertHexToPhysical(ref TASKMessage msg)
        {
            Parameter param = null;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<Parameter> EFUSEParamList = new List<Parameter>();
            List<Parameter> OpParamList = new List<Parameter>();

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                switch (p.guid & ElementDefine.SectionMask)
                {
                    case ElementDefine.EFUSEElement:
                        {
                            if (p == null) break;
                            EFUSEParamList.Add(p);
                            break;
                        }
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            OpParamList.Add(p);
                            break;
                        }
                }
            }

            if (EFUSEParamList.Count != 0)
            {
                for (int i = 0; i < EFUSEParamList.Count; i++)
                {
                    param = (Parameter)EFUSEParamList[i];
                    if (param == null) continue;

                    m_parent.Hex2Physical(ref param);
                }
            }

            if (OpParamList.Count != 0)
            {
                for (int i = 0; i < OpParamList.Count; i++)
                {
                    param = (Parameter)OpParamList[i];
                    if (param == null) continue;

                    m_parent.Hex2Physical(ref param);
                }
            }

            return ret;
        }

        public UInt32 ConvertPhysicalToHex(ref TASKMessage msg)
        {
            Parameter param = null;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<Parameter> EFUSEParamList = new List<Parameter>();
            List<Parameter> OpParamList = new List<Parameter>();

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            List<Parameter> virtualparamlist = new List<Parameter>();

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                switch (p.guid & ElementDefine.SectionMask)
                {
                    case ElementDefine.EFUSEElement:
                        {
                            if (p == null) break;
                            EFUSEParamList.Add(p);
                            break;
                        }
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            OpParamList.Add(p);
                            break;
                        }
                }
            }


            if (EFUSEParamList.Count != 0)
            {
                for (int i = 0; i < EFUSEParamList.Count; i++)
                {
                    param = (Parameter)EFUSEParamList[i];
                    if (param == null) continue;
                    //if ((param.guid & ElementDefine.ElementMask) == ElementDefine.TemperatureElement) continue;

                    m_parent.Physical2Hex(ref param);
                }
            }

            if (OpParamList.Count != 0)
            {
                for (int i = 0; i < OpParamList.Count; i++)
                {
                    param = (Parameter)OpParamList[i];
                    if (param == null) continue;

                    m_parent.Physical2Hex(ref param);
                }
            }

            return ret;
        }
        public UInt32 Command(ref TASKMessage msg)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            switch ((ElementDefine.COMMAND)msg.sub_task)
            {

                case ElementDefine.COMMAND.FROZEN_BIT_CHECK:
                    ret = FrozenBitCheck();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;

                case ElementDefine.COMMAND.DIRTY_CHIP_CHECK:
                    ret = DirtyChipCheck();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;

                case ElementDefine.COMMAND.DOWNLOAD_WITH_POWER_CONTROL:
                    {
                        ret = Download(ref msg, true);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
#if debug
                        Thread.Sleep(1000);
#endif
                        break;
                    }

                case ElementDefine.COMMAND.DOWNLOAD_WITHOUT_POWER_CONTROL:
                    {
                        ret = Download(ref msg, false);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
#if debug
                        Thread.Sleep(1000);
#endif
                        break;
                    }
                case ElementDefine.COMMAND.READ_BACK_CHECK:
                    {
                        ret = ReadBackCheck();
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        break;
                    }
                case ElementDefine.COMMAND.SAVE_EFUSE_HEX:
                    {
                        InitEfuseData();
                        ret = ConvertPhysicalToHex(ref msg);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        PrepareHexData();
                        ret = GetEfuseHexData(ref msg);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        FileStream hexfile = new FileStream(msg.sub_task_json, FileMode.Create);
                        StreamWriter hexsw = new StreamWriter(hexfile);
                        hexsw.Write(msg.sm.efusehexdata);
                        hexsw.Close();
                        hexfile.Close();

                        ret = GetEfuseBinData(ref msg);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;

                        string binfilename = Path.Combine(Path.GetDirectoryName(msg.sub_task_json),
                            Path.GetFileNameWithoutExtension(msg.sub_task_json) + ".bin");

                        Encoding ec = Encoding.UTF8;
                        using (BinaryWriter bw = new BinaryWriter(File.Open(binfilename, FileMode.Create), ec))
                        {
                            foreach (var b in msg.sm.efusebindata)
                                bw.Write(b);

                            bw.Close();
                        }
                        break;
                    }
                case ElementDefine.COMMAND.BIN_FILE_CHECK:
                    {
                        string binFileName = msg.sub_task_json;

                        var blist = SharedAPI.LoadBinFileToList(binFileName);
                        if (blist.Count == 0)
                            ret = LibErrorCode.IDS_ERR_DEM_LOAD_BIN_FILE_ERROR;
                        else
                            ret = CheckBinData(blist);
                        break;
                    }

            }
            return ret;
        }
        public uint CheckBinData(List<byte> blist)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            int length = (ElementDefine.EF_USR_TOP - ElementDefine.EF_USR_OFFSET + 1);
            length *= 2;    //一个字节地址，两个字节数值
            if (blist.Count != length)
            {
                ret = LibErrorCode.IDS_ERR_DEM_BIN_LENGTH_ERROR;
            }
            else
            {
                for (int i = ElementDefine.EF_USR_OFFSET, j = 0; i <= ElementDefine.EF_USR_TOP; i++, j++)
                {
                    if (blist[j * 2] != i)
                    {
                        ret = LibErrorCode.IDS_ERR_DEM_BIN_ADDRESS_ERROR;
                        break;
                    }
                }
            }
            return ret;
        }

        private void InitEfuseData()
        {
            for (ushort i = ElementDefine.EF_USR_OFFSET; i <= ElementDefine.EF_USR_TOP; i++)
            {
                parent.m_EFRegImg[i].err = 0;
                parent.m_EFRegImg[i].val = 0;
            }
        }

        private UInt32 GetEfuseHexData(ref TASKMessage msg)
        {
            string tmp = "";
            for (ushort i = ElementDefine.EF_USR_OFFSET; i <= ElementDefine.EF_USR_TOP; i++)
            {
                if (parent.m_EFRegImg[i].err != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return parent.m_EFRegImg[i].err;
                tmp += "0x" + i.ToString("X2") + ", " + "0x" + parent.m_EFRegImg[i].val.ToString("X2") + "\r\n";
            }
            msg.sm.efusehexdata = tmp;
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }

        private UInt32 GetEfuseBinData(ref TASKMessage msg)
        {
            List<byte> tmp = new List<byte>();
            for (ushort i = ElementDefine.EF_USR_OFFSET; i <= ElementDefine.EF_USR_TOP; i++)
            {
                if (parent.m_EFRegImg[i].err != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return parent.m_EFRegImg[i].err;
                tmp.Add((byte)i);
                tmp.Add((byte)(parent.m_EFRegImg[i].val));
            }
            msg.sm.efusebindata = tmp;
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }

        private UInt32 FrozenBitCheck() //注意，这里没有把image里的Frozen bit置为1，记得在后面的流程中做这件事
        {
#if frozen
            return LibErrorCode.IDS_ERR_DEM_FROZEN;
#else
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            byte pval = 0;
            ret = OnReadByte((byte)ElementDefine.EF_USR_TOP, ref pval);
            if ((pval & 0x01) == 0x01)
            {
                return LibErrorCode.IDS_ERR_DEM_FROZEN;
            }

            return ret;
#endif
        }

        private UInt32 DirtyChipCheck()
        {
#if dirty
            return LibErrorCode.IDS_ERR_DEM_DIRTYCHIP;
#else
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            byte pval = 0;
            for (byte index = (byte)ElementDefine.EF_USR_OFFSET; index <= (byte)ElementDefine.EF_USR_TOP; index++)
            {
                if (index == (byte)ElementDefine.CELL_NUM_OFFSET)      //略过cell number. Issue 978
                    continue;
                ret = OnReadByte(index, ref pval);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    return ret;
                }
                else if (pval != 0)
                {
                    return LibErrorCode.IDS_ERR_DEM_DIRTYCHIP;
                }
            }
            return ret;
#endif
        }

        private void PrepareHexData()
        {
            parent.m_EFRegImg[ElementDefine.EF_USR_TOP].val |= 0x0001;    //Set Frozen bit in image

            byte[] usrbuf = new byte[ElementDefine.USR_CRC_BUF_LEN];
            GetUSRCRCRef(ref usrbuf);
            parent.m_EFRegImg[ElementDefine.USR_CRC_OFFSET].val = CalEFUSECRC(usrbuf, ElementDefine.USR_CRC_BUF_LEN);

        }

        private UInt32 Download(ref TASKMessage msg, bool isWithPowerControl)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            ret = SetWorkMode(ElementDefine.WORK_MODE.PROGRAM);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return ret;
            }

            if (isWithPowerControl)
            {
                ret = PowerOn();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
            }

            LoadEFRegImgFromEFUSEBin(msg.sm.efusebindata);

            for (byte badd = (byte)ElementDefine.EF_USR_OFFSET; badd <= (byte)ElementDefine.EF_USR_TOP; badd++)
            {
                if (badd != ElementDefine.CELL_NUM_OFFSET)                      //略过cell number，已经写了，就不要再写了
                    ret = OnWriteByte(badd, (byte)EFUSEUSRbuf[badd - ElementDefine.EF_USR_OFFSET]);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    return ret;
                }
            }

            if (isWithPowerControl)
            {
                ret = PowerOff();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
            }

            ret = SetWorkMode(ElementDefine.WORK_MODE.NORMAL);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return ret;
            }

            return ret;
        }


        private void LoadEFRegImgFromEFUSEBin(List<byte> efusebindata)
        {
            EFUSEUSRbuf[0] = efusebindata[1];
            EFUSEUSRbuf[1] = efusebindata[3];
            EFUSEUSRbuf[2] = efusebindata[5];
            EFUSEUSRbuf[3] = efusebindata[7];
            EFUSEUSRbuf[4] = efusebindata[9];
            EFUSEUSRbuf[5] = efusebindata[11];
            EFUSEUSRbuf[6] = efusebindata[13];
            EFUSEUSRbuf[7] = efusebindata[15];
        }

        private UInt32 ReadBackCheck()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
#if readback
            return LibErrorCode.IDS_ERR_DEM_BUF_CHECK_FAIL;
#else
            byte pval = 0;
            for (byte badd = (byte)ElementDefine.EF_USR_OFFSET; badd <= (byte)ElementDefine.EF_USR_TOP; badd++)
            {
                ret = OnReadByte(badd, ref pval);
                if (pval != (byte)EFUSEUSRbuf[badd - ElementDefine.EF_USR_OFFSET])
                {
                    return LibErrorCode.IDS_ERR_DEM_BUF_CHECK_FAIL;
                }
            }
            return ret;
#endif
        }
        public UInt32 EpBlockRead()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = SetWorkMode(ElementDefine.WORK_MODE.MAPPING);
            return ret;
        }
        #endregion

        #region 特殊服务功能设计
        public UInt32 GetDeviceInfor(ref DeviceInfor deviceinfor)
        {

#if debug
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
#else
            string shwversion = String.Empty;
            byte bval = 0;
            byte cell_num = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            ret = ReadByte((byte)ElementDefine.CELL_NUM_OFFSET, ref bval);     //读Efuse中的cell number
            parent.m_EFRegImg[ElementDefine.CELL_NUM_OFFSET].err = ret;
            parent.m_EFRegImg[ElementDefine.CELL_NUM_OFFSET].val = bval;
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;
            bval &= 0x03;                       //保留cell number 所在的后两位
            switch (bval)
            {
                case 0:
                case 1:
                    cell_num = 3;
                    break;
                case 2:
                    cell_num = 4;
                    break;
                case 3:
                    cell_num = 5;
                    break;
            }
            deviceinfor.type = cell_num;
            foreach (UInt16 type in deviceinfor.pretype)
            {
                if (SharedFormula.LoByte(type) != deviceinfor.type)
                    ret = LibErrorCode.IDS_ERR_DEM_BETWEEN_SELECT_BOARD;

                if (ret == LibErrorCode.IDS_ERR_SUCCESSFUL) break;
            }
            deviceinfor.status = 0;     //N变成Y

            return ret;
#endif
        }

        public UInt32 GetSystemInfor(ref TASKMessage msg)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            return ret;
        }

        public UInt32 GetRegisteInfor(ref TASKMessage msg)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            return ret;
        }
        #endregion
    }
}