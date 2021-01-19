using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cobra.Common;

namespace Cobra.KALL
{
    /// <summary>
    /// 数据结构定义
    ///     XX       XX        XX         XX
    /// --------  -------   --------   -------
    ///    保留   参数类型  寄存器地址   起始位
    /// </summary>
    internal class ElementDefine
    {
        #region Chip Constant
        internal const UInt16 EF_MEMORY_SIZE = 0x10;
        internal const UInt16 EF_MEMORY_OFFSET = 0x10;
        internal const UInt16 EF_ATE_OFFSET = 0x10;
        internal const UInt16 EF_ATE_TOP = 0x17;
        internal const UInt16 ATE_CRC_OFFSET = 0x16;

        internal const UInt16 EF_USR_OFFSET = 0x18;
        internal const UInt16 EF_USR_TOP = 0x1f;
        internal const UInt16 USR_CRC_OFFSET = 0x1e;

        internal const UInt16 ATE_CRC_BUF_LEN = 6;     // 00~05
        internal const UInt16 USR_CRC_BUF_LEN = 5;     // 08~0c

        internal const UInt16 CELL_NUM_OFFSET = 0x1d;

        internal const UInt16 OP_MEMORY_SIZE = 0xFF;
        internal const UInt16 PARAM_HEX_ERROR = 0xFFFF;
        internal const Double PARAM_PHYSICAL_ERROR = -9999;

        internal const int RETRY_COUNTER = 15;
        internal const byte WORKMODE_OFFSET = 0x30;

        #region 温度参数GUID
        //internal const UInt32 TemperatureElement = 0x00010000;
        //internal const UInt32 TpETRx = TemperatureElement + 0x00;
        #endregion
        internal const UInt32 SectionMask = 0xffff0000;

        //To be reviewed
        #region EFUSE参数GUID
        internal const UInt32 EFUSEElement = 0x00020000; //EFUSE参数起始地址
        internal const UInt32 ECTO = 0x0002680e; //
        internal const UInt32 ECUT = 0x00026c00; //
        internal const UInt32 ECUT_H = 0x00026c06;
        internal const UInt32 EDUT = 0x00026c0a; //
        internal const UInt32 ECOT = 0x00026d00; //
        internal const UInt32 ECOT_H = 0x00026d08;
        internal const UInt32 EDOT = 0x00026e00; //
        internal const UInt32 EDOT_H = 0x00026e08;
        internal const UInt32 EDUT_H = 0x00026f04;
        internal const UInt32 EEOC = 0x00026f08; //

        //To be reviewed
        #endregion
        #region Operation参数GUID
        internal const UInt32 OperationElement = 0x00030000;

        //To be reviewed
        #endregion

        #endregion
        internal enum SUBTYPE : ushort
        {
            DEFAULT = 0,
            CELLNUMBER = 1,
            CELLNUMBER_04 = 2,
            CELLNUMBER_03 = 3,
        }

        #region Local ErrorCode
        internal const UInt32 IDS_ERR_DEM_POWERON_FAILED = LibErrorCode.IDS_ERR_SECTION_DYNAMIC_DEM + 0x0001;
        internal const UInt32 IDS_ERR_DEM_POWEROFF_FAILED = LibErrorCode.IDS_ERR_SECTION_DYNAMIC_DEM + 0x0002;
        internal const UInt32 IDS_ERR_DEM_POWERCHECK_FAILED = LibErrorCode.IDS_ERR_SECTION_DYNAMIC_DEM + 0x0003;
        internal const UInt32 IDS_ERR_DEM_ATE_EMPTY_CHECK_FAILED = LibErrorCode.IDS_ERR_SECTION_DYNAMIC_DEM + 0x0004; 
        #endregion
        internal enum WORK_MODE : byte
        {
            NORMAL = 0,
            INTERNAL = 0x01,
            PROGRAM = 0x02,
            MAPPING = 0x03
        }

        internal enum COMMAND : ushort
        {
            TESTCTRL_SLOP_TRIM = 2,
            FROZEN_BIT_CHECK = 9,
            DIRTY_CHIP_CHECK = 10,
            DOWNLOAD_WITH_POWER_CONTROL = 11,
            DOWNLOAD_WITHOUT_POWER_CONTROL = 12,
            READ_BACK_CHECK = 13,
            ATE_CRC_CHECK = 14,
            //GET_EFUSE_HEX_DATA = 15,          //Production make Hex file, no need anymore
            //SAVE_MAPPING_HEX = 16,            //Register make Hex file, no need anymore
            SAVE_EFUSE_HEX = 17,
            //GET_MAX_VALUE = 18,
            //GET_MIN_VALUE = 19,
            //VERIFICATION = 20,                   //Production页面的Read Back Check按钮，比 READ_BACK_CHECK 命令多一些动作
            BIN_FILE_CHECK = 21,                   //检查bin文件的合法性
            //ATE_EMPTY_CHECK = 22                   //检查ATE区域是否为空
        }
    }
}
