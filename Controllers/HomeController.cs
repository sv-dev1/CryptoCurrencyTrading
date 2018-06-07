using APILibrary.Bitfinex;
using APILibrary.Marketcap;
using BinanceExchange.API.Client;
using BinanceExchange.API.Client.Interfaces;
using BinanceExchange.API.Enums;
using BinanceExchange.API.Models.Request;
using BinanceExchange.API.Models.WebSocket;
using BinanceExchange.API.Utility;
using BinanceExchange.API.Websockets;
using Crypto.Models;
using CryptoCurrencyTrading.Models;
using Info.Blockchain.API.Client;
using Info.Blockchain.API.Models;
using Info.Blockchain.API.Wallet;
//using Info.Blockchain.API.Client;
//using Info.Blockchain.API.Models;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using Xunit;

namespace CryptoTrading.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        string apicode = WebConfigurationManager.AppSettings["ApiCode"];
        string ApiURL = WebConfigurationManager.AppSettings["ApiURL"];
        CryptoCurrencyTradingEntities db = new CryptoCurrencyTradingEntities();
        //string apiKey = "HKNIeFFUSCIfca2F5B2GA56UqLkBIIYJ7o01JNerIJz6XT8s1k0Mqg2c2PXdn842";
        //string secretKey = "ynM4OWcqRitNkmcs0gOaU1X5Z4jcxkhSgpzc0G3bGYrzCBsfoaP2xg9I9tgyY5Gu";

        public ActionResult Index()
        {
            DashboardModel model = new DashboardModel();
            var response = MCAPI.Get<JObject>("ticker/?limit=6");
            var tickers = MCAPI.GetData(response);
            model.tickersList = tickers;
            var symbols = MCAPI.Get<Symbols>("listings/");
            Session["TotalCurrency"] = symbols.metadata.num_cryptocurrencies;

            model.SymbolsList = symbols;
            return View(model);
        }


        public async Task<ActionResult> SendPayment_SendBtc_NoFreeOutputs()
        {
            var emailaddress = Session["Email_Address"];
            var walletdetails = db.AspNetUsers.Join(db.WalletDetails, u => u.Id, uir => uir.UserId, (u, uir) => new { u, uir }).Where(w => w.u.Email == emailaddress.ToString()).Select(s => new { s.uir.Address, s.uir.GuidIdentifier }).FirstOrDefault();



            string WALLET_ID = walletdetails.GuidIdentifier;
            string Form_Address = walletdetails.Address;
            string WALLET_PASSWORD = "Kindle@123";
            string FIRST_ADDRESS = "16RQNCzpCQAyTQkj2Bp43vuBu5D822bGAn";

           

            ServerApiException apiException = await Assert.ThrowsAsync<ServerApiException>(async () =>
            {
                BlockchainHttpClient httpClient = new BlockchainHttpClient(apicode, ApiURL);
                using (BlockchainApiHelper apiHelper = new BlockchainApiHelper(apicode, httpClient, ApiURL, httpClient))
                {
                    Wallet wallet = apiHelper.InitializeWallet(WALLET_ID, WALLET_PASSWORD);
                    await wallet.SendAsync(FIRST_ADDRESS, BitcoinValue.FromBtc(1), Form_Address, BitcoinValue.FromBtc(0));
                }
            });
            Assert.Contains("No free", apiException.Message);

            return RedirectToAction("Index");
        }

        public async Task<ActionResult> CreateWallet()
        {
            try
            {
                BlockchainHttpClient httpClient = new BlockchainHttpClient(apicode, ApiURL);
                using (BlockchainApiHelper apiHelper = new BlockchainApiHelper(apicode, httpClient,
                                                            "ApiURL", httpClient))
                {
                    var emailaddress = Session["Email_Address"] == null ? null : Session["Email_Address"].ToString();
                    var password = Session["Password"] == null ? null : Session["Password"].ToString();
                    if (string.IsNullOrEmpty(emailaddress) && string.IsNullOrEmpty(password))
                    {
                        return RedirectToAction("Login", "Account");
                    }
                    else
                    {
                        WalletDetail ObjWalletViewModel = new WalletDetail();
                        var userid = db.AspNetUsers.Where(w => w.Email == emailaddress).Select(s => s.Id).FirstOrDefault();
                        CreateWalletResponse walletResponse = await apiHelper.walletCreator.CreateAsync(password, null, null, emailaddress);  //apiHelper.walletCreator.CreateAsync("Kindle@123");
                        ObjWalletViewModel.GuidIdentifier = walletResponse.Identifier;
                        ObjWalletViewModel.UserId = userid;
                        ObjWalletViewModel.Address = walletResponse.Address;
                        db.WalletDetails.Add(ObjWalletViewModel);
                        db.SaveChanges();
                        return RedirectToAction("Index");
                    }
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public ActionResult CurrencyList()
        {

            List<Ticklers> parentTickers = new List<Ticklers>();
            var response = MCAPI.Get<JObject>("ticker/?limit=100");
            var tickers = MCAPI.GetData(response);
            double currencyTotal = 0.00;
            if (Session["TotalCurrency"] == null)
            {
                var symbols = MCAPI.Get<Symbols>("listings/");
                currencyTotal = symbols.metadata.num_cryptocurrencies;
            }
            else
            {
                currencyTotal = Convert.ToInt32(Session["TotalCurrency"]);
            }
            parentTickers.AddRange(tickers);

            double remaining = currencyTotal / 100;
            int intPart = Convert.ToInt32(remaining);
            double decimalPart = remaining - intPart;
            if (decimalPart > 0)
            {
                intPart++;
            }
            int start = 101;
            for (int i = 1; i < intPart; i++)
            {
                response = MCAPI.Get<JObject>($"/ticker/?start={start}&limit=100");
                tickers = MCAPI.GetData(response);
                parentTickers.AddRange(tickers);
                start = start + 100;
            }
            return View(parentTickers);
        }
        public ActionResult CurrencyDetail(int symbol)
        {
            var response = MCAPI.Get<JObject>("ticker/" + symbol);
            var tickers = MCAPI.GetSysmbolData(response);
            if (symbol == 1)
            {
                var response_trade = Bitfinex.GetTicklers("candles/trade:1M:tBTCUSD/hist");
                JsonSerializerSettings _jsonSetting = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
                ViewBag.DataPoints = JsonConvert.SerializeObject(response_trade.ListChartParameters.ToList(), _jsonSetting);
                tickers.ListChartParameters = response_trade.ListChartParameters;
            }
            else if (symbol == 1027)
            {
                var response_trade = Bitfinex.GetTicklers("candles/trade:1M:tETHUSD/hist");
                JsonSerializerSettings _jsonSetting = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
                ViewBag.DataPoints = JsonConvert.SerializeObject(response_trade.ListChartParameters.ToList(), _jsonSetting);
                tickers.ListChartParameters = response_trade.ListChartParameters;
            }

            return View(tickers);
        }


        public ActionResult _ChartTable(int sym, string selectedval)
        {
            var response = MCAPI.Get<JObject>("ticker/" + 1);
            var symbol = "";
            var tickers = MCAPI.GetSysmbolData(response);
            var response_trade = new Ticklers();

            if (sym == 1)
            {
                symbol = "tBTCUSD";
            }
            else if (sym == 2)
            {
                symbol = "tETHUSD";
            }

            if (selectedval == "1Min")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 1 + "m:" + symbol + "/hist");
            }

            else if (selectedval == "1Hour")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 1 + "h:" + symbol + "/hist");
            }
            else if (selectedval == "1Day")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 1 + "D:" + symbol + "/hist");
            }
            else if (selectedval == "7Days")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 7 + "D:" + symbol + "/hist");
            }
            else if (selectedval == "14Days")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 14 + "D:" + symbol + "/hist");
            }
            else if (selectedval == "1Month")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 1 + "M:" + symbol + "/hist");
            }
            JsonSerializerSettings _jsonSetting = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
            ViewBag.DataPoints = JsonConvert.SerializeObject(response_trade.ListChartParameters.ToList(), _jsonSetting);
            return PartialView(tickers);
        }


        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }
        public ActionResult CalculatePrice(string sourceId, string destinationSymbol)
        {
            var symbols = MCAPI.Get<ValueConversion>($"ticker/{sourceId}/?convert={destinationSymbol}", destinationSymbol);
            JsonResult jsonResult = new JsonResult();
            jsonResult.Data = symbols.data;
            jsonResult.JsonRequestBehavior = JsonRequestBehavior.AllowGet;
            return jsonResult;
        }
        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }


        public ActionResult _HistoricalTable(string drpdownval, int sym = 1)
        {

            var response = MCAPI.Get<JObject>("ticker/" + 1);
            var symbol = "";
            var tickers = MCAPI.GetSysmbolData(response);
            var response_trade = new Ticklers();

            if (sym == 1)
            {
                symbol = "tBTCUSD";
            }
            else if (sym == 2)
            {
                symbol = "tETHUSD";
            }


            if (drpdownval == "1 Min")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 1 + "m:" + symbol + "/hist");
            }

            else if (drpdownval == "1 Hour")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 1 + "h:" + symbol + "/hist");
            }

            else if (drpdownval == "30 Days")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 1 + "M:" + symbol + "/hist");
            }
            else if (drpdownval == "7 Days")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 7 + "D:" + symbol + "/hist");
            }
            else if (drpdownval == "1 Day")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 1 + "D:" + symbol + "/hist");
            }
            else if (drpdownval == "14 Days")
            {
                response_trade = Bitfinex.GetTicklers("candles/trade:" + 14 + "D:" + symbol + "/hist");
            }

            tickers.ListChartParameters = response_trade.ListChartParameters;
            return PartialView(tickers);
        }

    }
}