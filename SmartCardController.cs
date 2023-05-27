using AyodiaSmartCard.Model;
using AyodiaSmartCard.ThaiIdCard;
using PCSC;
using PCSC.Exceptions;
using PCSC.Iso7816;
using PCSC.Monitoring;
using PCSC.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Web.Http;

namespace AyodiaSmartCard
{
    public class SmartCardController : ApiController
    {

        const int ECODE_SCardError = 256;
        const int ECODE_UNSUPPORT_CARD = 1;

        private readonly IContextFactory _contextFactory;
        private ISCardContext _hContext;
        private SCardReader _reader;
        private SCardError _err;
        private IntPtr _pioSendPci;

        private IAPDU_THAILAND_IDCARD _apdu;
        private SCardMonitor _monitor;

        private string _error_message;
        private int _error_code;
        public SmartCardController()
        {
            _contextFactory = ContextFactory.Instance;

        }
        private static bool IsEmpty(ICollection<string> readerNames) => readerNames == null || readerNames.Count == 0;

        private string GetUTF8FromAsciiBytes(byte[] ascii_bytes)
        {
            byte[] utf8;
            utf8 = Encoding.Convert(
                Encoding.GetEncoding("TIS-620"),
                Encoding.UTF8,
                ascii_bytes
                );
            return Encoding.UTF8.GetString(utf8);
        }
        private byte[] SendCommand(byte[] command)
        {
            byte[] pbRecvBuffer;
            pbRecvBuffer = new byte[256];
            _err = _reader.Transmit(_pioSendPci, command, ref pbRecvBuffer);
            CheckErr(_err);
            var responseApdu = new ResponseApdu(pbRecvBuffer, IsoCase.Case2Short, _reader.ActiveProtocol);

            if (responseApdu.SW1.Equals((byte)SW1Code.NormalDataResponse))
            {
                command = _apdu.APDU_GET_RESPONSE().Concat(new byte[] { responseApdu.SW2 }).ToArray();
                pbRecvBuffer = new byte[258];
                _err = _reader.Transmit(_pioSendPci, command, ref pbRecvBuffer);
                if (pbRecvBuffer.Length - responseApdu.SW2 == 2)
                {
                    return pbRecvBuffer.Take(pbRecvBuffer.Length - 2).ToArray();
                }
            }

            return pbRecvBuffer;
        }
        private void CheckErr(SCardError _err)
        {
            if (_err != SCardError.Success)
                throw new PCSCException(_err,
                    SCardHelper.StringifyError(_err));
        }

        private bool Open(string readerName = null)
        {
            try
            {
                // delay 1.5 second for ATR reading.
                //Thread.Sleep(1500);

                _hContext = _contextFactory.Establish(SCardScope.System);
                _reader = new SCardReader(_hContext);

                // Connect to the card
                if (String.IsNullOrEmpty(readerName))
                {
                    // Open first avaliable reader.
                    // Retrieve the list of Smartcard _readers
                    string[] szReaders = _hContext.GetReaders();
                    if (szReaders.Length <= 0)
                        throw new PCSCException(SCardError.NoReadersAvailable,
                            "Could not find any Smartcard _reader.");

                    _err = _reader.Connect(szReaders[0],
                                SCardShareMode.Shared,
                                SCardProtocol.T0 | SCardProtocol.T1);
                    CheckErr(_err);
                }
                else
                {
                    _err = _reader.Connect(readerName,
                                SCardShareMode.Shared,
                                SCardProtocol.T0 | SCardProtocol.T1);
                    CheckErr(_err);
                }


                _pioSendPci = new IntPtr();
                switch (_reader.ActiveProtocol)
                {
                    case SCardProtocol.T0:
                        _pioSendPci = SCardPCI.T0;
                        break;
                    case SCardProtocol.T1:
                        _pioSendPci = SCardPCI.T1;
                        break;
                    case SCardProtocol.Raw:
                        _pioSendPci = SCardPCI.Raw;
                        break;
                    default:
                        throw new PCSCException(SCardError.ProtocolMismatch,
                            "Protocol not supported: "
                            + _reader.ActiveProtocol.ToString());
                }

                string[] readerNames;
                SCardProtocol proto;
                SCardState state;
                byte[] atr;

                var sc = _reader.Status(
                    out readerNames,    // contains the reader name(s)
                    out state,          // contains the current state (flags)
                    out proto,          // contains the currently used communication protocol
                    out atr);           // contains the ATR

                if (atr == null || atr.Length < 2)
                {
                    return false;
                }

                if (atr[0] == 0x3B && atr[1] == 0x67)
                {
                    /* corruption card */
                    _apdu = new APDU_THAILAND_IDCARD_TYPE_01();
                }
                else
                {
                    _apdu = new APDU_THAILAND_IDCARD_TYPE_02();
                }

                // select MOI Applet
                if (SelectApplet())
                {
                    return true;
                }
                else
                {
                    _error_code = ECODE_UNSUPPORT_CARD;
                    _error_message = "SmartCard not support(Cannot select Ministry of Interior Applet.)";
                    return false;
                }


            }
            catch (PCSCException ex)
            {
                _error_code = ECODE_SCardError;
                _error_message = "Open Err: " + ex.Message + " (" + ex.SCardError.ToString() + ")";
                Debug.Print(_error_message);
                return false;
            }
        }

        private bool SelectApplet()
        {
            byte[] command = _apdu.APDU_SELECT(_apdu.AID_MOI);
            byte[] pbRecvBuffer;
            pbRecvBuffer = new byte[256];
            _err = _reader.Transmit(_pioSendPci, command, ref pbRecvBuffer);
            CheckErr(_err);
            var responseApdu = new ResponseApdu(pbRecvBuffer, IsoCase.Case2Short, _reader.ActiveProtocol);

            if (responseApdu.SW1.Equals((byte)SW1Code.NormalDataResponse) || responseApdu.SW1.Equals((byte)SW1Code.Normal))
            {
                return true;
            }

            return false;
        }

        private bool Close()
        {
            try
            {
                _reader.Disconnect(SCardReaderDisposition.Leave);
                _hContext.Release();
                return true;
            }
            catch (PCSCException ex)
            {
                _error_code = ECODE_SCardError;
                _error_message = "Close Err: " + ex.Message + " (" + ex.SCardError.ToString() + ")";
                Debug.Print(_error_message);
                return false;
            }
        }

        [HttpGet]
        public SmartCard Detect()
        {
            var result = new SmartCard();
            result.Name = string.Empty;
            result.Card = false;
            try
            {
                Log.Information("Detect id card...");
                var contextFactory = ContextFactory.Instance;
                using (var context = contextFactory.Establish(SCardScope.System))
                {
                    var readersNames = context.GetReaders();
                    if (!IsEmpty(readersNames))
                    {
                        result.Name = readersNames.Last();
                        if (!String.IsNullOrEmpty(result.Name))
                        {

                            Log.Information($"Reader Name : {result.Name}");
                            if (Open(result.Name))
                            {
                                result.Card = true;
                                Log.Information($"Reader Card : true");
                                Personal personal = new Personal();
                                // CID
                                personal.Citizenid = GetUTF8FromAsciiBytes(SendCommand(_apdu.EF_CID));

                                // Fullname Thai + Eng + BirthDate + Sex
                                personal.Info = GetUTF8FromAsciiBytes(SendCommand(_apdu.EF_PERSON_INFO));

                                // Address
                                personal.Address = GetUTF8FromAsciiBytes(SendCommand(_apdu.EF_ADDRESS));

                                // issue/expire
                                personal.Issue_Expire = GetUTF8FromAsciiBytes(SendCommand(_apdu.EF_CARD_ISSUE_EXPIRE));

                                // issuer
                                personal.Issuer = GetUTF8FromAsciiBytes(SendCommand(_apdu.EF_CARD_ISSUER));

                                //if (with_photo)
                                //{
                                //    // Photo
                                //    personal.PhotoRaw = SendPhotoCommand();
                                //}
                                result.Person = personal;
                                Close();

                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Information("Error: Detect id card!");
                Log.Information($"Error: {ex.Message}");
                Log.Information($"Error: ---------------------------");
                Log.Information($"Error: {ex.StackTrace}");
                return result;
            }
        }

        [HttpGet]
        public string GetValue()
        {
            //var contextFactory = ContextFactory.Instance;
            //using (var context = contextFactory.Establish(SCardScope.System))
            //{
            //    var readerNames = context.GetReaders();

            //    if (IsEmpty(readerNames))
            //    {
            //        return "You need at least one reader in order to run this example.";
            //    }
            //    using (var reader = context.ConnectReader(readerNames.Last(), SCardShareMode.Shared, SCardProtocol.Any))
            //    {
            //        //DisplayAtr(reader);
            //        var atr = reader.GetAttrib(SCardAttribute.AtrString);
            //        return BitConverter.ToString(atr ?? new byte[] { });
            //    }
            //    //DisplayAtrForEachReader(context, readerNames);
            //    //Console.ReadKey();
            //}
            return "";
        }
    }

    public class SmartCard
    {
        public string Name { get; set; }
        public bool Card { get; set; }
        public Personal Person { get; set; }
        //public Personal Person { get; set; }
    }
}
