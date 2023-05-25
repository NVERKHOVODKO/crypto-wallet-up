﻿using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UP.DTO;
using UP.Models;

namespace UP.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TransactionController: ControllerBase
    {
        private readonly ILogger<TransactionController> _logger;

        public TransactionController(ILogger<TransactionController> logger)
        {
            _logger = logger;
        }
        
        [HttpGet, Route("getCoinQuantity")]
        public async Task<ActionResult> GetCoinQuantity(string coinName, double quantityUSD)
        {
            var cr = new Repositories.CurrencyRepository();
            try
            {
                return Ok(await cr.GetCoinQuantity(quantityUSD, coinName));
            }
            catch(Exception)
            {
                _logger.LogInformation($"Не получилось вернуть количество монет");
                return BadRequest("Unable to return coins quantity");
            }
        }
        
        [HttpGet, Route("getUserConversationsHistory")]
        public async Task<ActionResult> GetUserList(int id)
        {
            var tr = new Repositories.TransactionsRepository();
            try
            {
                return Ok(tr.GetUserConversionsHistory(id));
            }
            catch(Exception)
            {
                _logger.LogInformation($"Unable to return user transactions history");
                return BadRequest("Не получилось вернуть историю транзакций пользователя");
            }
        }
        
        [HttpGet, Route("getUserDepositHistory")]
        public async Task<ActionResult> GetUserDepositHistory(int id)
        {
            try
            {
                var tr = new Repositories.TransactionsRepository();
                return Ok(tr.GetUserDepositHistory(id));            
            }
            catch(Exception)
            {
                _logger.LogInformation($"Unable to return user transactions history");
                return BadRequest("Не получилось вернуть историю транзакций пользовалетя");
            }
        }
        
        [HttpPost, Route("convert")]
        public async Task<ActionResult> Convert([FromBody] ConvertRequest request)
        {
            try
            {
                _logger.LogInformation($"User:" + request.UserId + "Converted " + request.Quantity + " " + request.ShortNameStart + " to " + request.ShortNameFinal);
                if (request.Quantity == 0)
                {
                    _logger.LogInformation($"Error. Quantity must be above than zero");
                    return BadRequest("Количество должно быть больше нуля");
                }
                if (request.Quantity < 0)
                {
                    _logger.LogInformation($"Error. Quantity must be above than zero");
                    return BadRequest("Количество должно быть больше нуля");
                }
                string apiKey = "4da2c4791b9c285b22c1bf08bc36f304ab2ca80bc901504742b9a42a814c4614";
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);
                string url = $"https://min-api.cryptocompare.com/data/price?fsym=" + request.ShortNameStart + "&tsyms=" + request.ShortNameFinal;
                var response = await httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(responseContent);
                double priceRatio =   (double)json[request.ShortNameFinal.ToUpper()];
                double finalQuantity = priceRatio * request.Quantity;
                var ur = new Repositories.UserRepository();
                double startCoinQuantityInUserWallet = ur.GetCoinQuantityInUserWallet(request.UserId, request.ShortNameStart);
                if (startCoinQuantityInUserWallet < request.Quantity)
                {
                    _logger.LogInformation($"The user doesn't have enough coins to complete the conversion");
                    return BadRequest("Недостаточно монет для совершения конвертации");
                }
                var cr = new Repositories.CurrencyRepository();
                cr.SubtractCoinFromUser(request.UserId, request.ShortNameStart, request.Quantity);
                cr.AddCryptoToUserWallet(request.UserId, request.ShortNameFinal, finalQuantity);
                var tr = new Repositories.TransactionsRepository();
                tr.WriteNewConversionDataToDatabase(new Conversion(1, 0, request.Quantity, finalQuantity, await cr.GetCoinPrice(request.Quantity, request.ShortNameStart),
                    request.ShortNameStart, request.ShortNameFinal, request.UserId, DateTime.Now));
                _logger.LogInformation($"Converted successfully");
                return Ok("Конвертация совершена испешно");
            }
            catch (Exception e)
            {
                _logger.LogInformation($"Error. Currencies have not been converted");
                return BadRequest("Валюты не были конвертированы");
            }
        }
        

        /*public async Task<double> GetPriceRatio(string shortNameStart, string shortNameFinal)
        {
            string apiKey = "4da2c4791b9c285b22c1bf08bc36f304ab2ca80bc901504742b9a42a814c4614";
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);
            string url = $"https://min-api.cryptocompare.com/data/price?fsym=" + shortNameStart + "&tsyms=" + shortNameFinal;
            var response = await httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(responseContent);

            return  (double)json[shortNameFinal.ToUpper()];
        }*/
        
        
        [HttpPost, Route("buyCrypto")]
        public async Task<ActionResult> BuyCrypto([FromBody] BuyCryptoRequest request)
        {
            try
            {
                _logger.LogInformation($"UserId:" + request.UserId + "\nCoin quantity:" + request.Quantity + "\nCoin name: " + request.CoinName);
                if (request.Quantity == 0)
                {
                    _logger.LogInformation($"Quantity must be above than zero");
                    return UnprocessableEntity("Количество должно быть больше нуля");
                }
                var ur = new Repositories.UserRepository();
                var cr = new Repositories.CurrencyRepository();
                double quantityUSDTInUserWallet = ur.GetCoinQuantityInUserWallet(request.UserId, "usdt");
                if (quantityUSDTInUserWallet < request.Quantity)
                {
                    _logger.LogInformation($"Not enough balance");
                    return UnprocessableEntity("Недостаточно монет");
                }
                cr.SubtractCoinFromUser(request.UserId, "usdt", request.Quantity);
                double coinQuantity = await cr.GetCoinQuantity(request.Quantity, request.CoinName);
                cr.AddCryptoToUserWallet(request.UserId, request.CoinName, coinQuantity);
                _logger.LogInformation($"UserId(" + request.UserId + ") bought " + coinQuantity + " " + request.CoinName);
                return Ok("Транзакция совершена успешно");
            }
            catch (Exception e)
            {
                _logger.LogInformation($"Transaction wasn't completed");
                return BadRequest("Транзакция не была совершена");
            }
        }
        
        
        [HttpPut, Route("sellCrypto")]
        public async Task<ActionResult> SellCrypto([FromBody] SellCryptoRequest request)
        {
            try
            {
                if (request.QuantityForSell == 0)
                {
                    _logger.LogInformation($"Quantity must be above than zero");
                    return UnprocessableEntity("Количество должно быть больше нуля");
                }
                if (request.QuantityForSell < 0)
                {
                    _logger.LogInformation($"Quantity must be above than zero");
                    return BadRequest("Количество должно быть больше нуля");
                }
                var ur = new Repositories.UserRepository();
                var cr = new Repositories.CurrencyRepository();
                double quantityInUserWallet = ur.GetCoinQuantityInUserWallet(request.UserId, request.CoinName);
                if (quantityInUserWallet < request.QuantityForSell)
                {
                    _logger.LogInformation($"Not enough coins");
                    return UnprocessableEntity("Недостаточно монет");
                }
                cr.SubtractCoinFromUser(request.UserId, request.CoinName, request.QuantityForSell);
                cr.AddCryptoToUserWallet(request.UserId, "usdt", await cr.GetCoinPrice(request.QuantityForSell, request.CoinName));
                return Ok("Транзакция совершена успешно");
            }
            catch (Exception e)
            {
                _logger.LogInformation($"Transaction wasn't completed");
                return BadRequest("Транзакция не была выполнена");
            }
        }
        
        [HttpPost, Route("sendCrypto")]
        public async Task<ActionResult> SendCrypto([FromBody] SendCryptoRequest request)
        {
            try
            {
                _logger.LogInformation($"Sended: " + request.QuantityForSend + " " + request.CoinName + " by user( " + request.SenderId + ") to user(" + request.ReceiverId + ")");
                if (request.ReceiverId == request.SenderId)
                {
                    _logger.LogInformation($"You can't send cryptocurrency to yourself");
                    return UnprocessableEntity("Невозможно отправить криптовалюту себе же");
                }
                if (request.QuantityForSend == 0)
                {
                    _logger.LogInformation($"Quantity must be above than zero");
                    return UnprocessableEntity("Количество должно быть больше нуля");
                }
                if (request.QuantityForSend < 0)
                {
                    _logger.LogInformation($"Quantity must be above than zero");
                    return UnprocessableEntity("Количество должно быть больше нуля");
                }
                var ur = new Repositories.UserRepository();
                var cr = new Repositories.CurrencyRepository();
                double quantityInUserWallet = ur.GetCoinQuantityInUserWallet(request.SenderId, request.CoinName);
                if (quantityInUserWallet < request.QuantityForSend)
                {
                    _logger.LogInformation($"Not enough coins");
                    return UnprocessableEntity("Недостаточно монет");
                }
                cr.SubtractCoinFromUser(request.SenderId, request.CoinName, request.QuantityForSend);
                cr.AddCryptoToUserWallet(request.ReceiverId, request.CoinName, request.QuantityForSend);
                cr.WriteTransactionToDatabase(request.CoinName, request.QuantityForSend, request.SenderId, request.ReceiverId);
                _logger.LogInformation($"Transfer completed successfully");
                return Ok("Перевод выполнен успешно");
            }
            catch (Exception e)
            {
                _logger.LogInformation($"Transfer wasn't completed");
                return BadRequest("Перевод не был выполнен");
            }
        }
        
        
        [HttpPost, Route("replenishTheBalance")]
        public async Task<ActionResult> ReplenishTheBalance([FromBody] DTO.ReplenishTheBalanceRequest request)
        {
            var tr = new Repositories.TransactionsRepository();
            _logger.LogInformation($"Replenishment from user(" + request.UserId + "): " + request.QuantityUsd + "$");
            try
            {
                if (request.QuantityUsd == null)
                {
                    return BadRequest("Количество должно быть больше нуля");
                }
                if (request.QuantityUsd == 0)
                {
                    return BadRequest("Количество должно быть больше нуля");
                }
                if (request.QuantityUsd < 0)
                {
                    return BadRequest("Количество должно быть больше нуля");
                }
                tr.ReplenishTheBalance(request.UserId, request.QuantityUsd);
                _logger.LogInformation($"Balance replenished successfully");
                return Ok("Баланс пополнен успешно");
            }
            catch(Exception)
            { 
                _logger.LogInformation($"Unable to replenish the balance");
                return BadRequest("Не получилось пополнить баланс");
            }
        }
        
        [HttpPut, Route("withdrawUSDT")]
        public async Task<ActionResult> WithdrawUSDT([FromBody] DTO.WithdrawRequest request)
        {
            var tr = new Repositories.TransactionsRepository();
            _logger.LogInformation($"Withdraw from user(" + request.UserId + "): " + request.QuantityForWithdraw + "$");
            try
            {
                if (request.QuantityForWithdraw == 0)
                {
                    return UnprocessableEntity("Количество должно быть больше нуля");
                }
                if (request.QuantityForWithdraw < 0)
                {
                    _logger.LogInformation($"Quantity must be above than zero");
                    return UnprocessableEntity("Количество должно быть больше нуля");
                }
                var ur = new Repositories.UserRepository();
                var cr = new Repositories.CurrencyRepository();
                double quantityInUserWallet = ur.GetCoinQuantityInUserWallet(request.UserId, "usdt");
                if (quantityInUserWallet < request.QuantityForWithdraw)
                {
                    _logger.LogInformation($"Not enough balance");
                    return UnprocessableEntity("Недостаточно монет");
                }
                cr.SubtractCoinFromUser(request.UserId, "usdt", request.QuantityForWithdraw);
                cr.WriteWithdrawToDatabase(request.QuantityForWithdraw, (request.QuantityForWithdraw * 0.02), request.UserId);
                return Ok("Транзакция выполнена успешно");
            }
            catch(Exception)
            {
                _logger.LogInformation($"Unable to withdraw the balance");
                return BadRequest("Не удалось вывести денежные средства");
            }
        }
        
        [HttpGet, Route("getUserWithdrawalsHistory")]
        public async Task<IActionResult> GetUserWithdrawalsHistory(int userId)
        {
            var tr = new Repositories.TransactionsRepository();
            try
            {
                return Ok(tr.GetUserWithdrawalsHistory(userId));
            }
            catch(Exception)
            {
                _logger.LogInformation($"Unable to get user withdrawals history");
                return BadRequest("Не удалось получить историю выводов");
            }
        }
        
        
        [HttpGet, Route("getUserTransactionsHistory")]
        public async Task<IActionResult> GetUserTransactionsHistory(int userId)
        {
            var tr = new Repositories.TransactionsRepository();
            try
            {
                return Ok(tr.GetUserTransactionsHistory(userId));
            }
            catch(Exception)
            {
                _logger.LogInformation($"Не удалось вернуть историю транзакций");
                return BadRequest("Не удалось вернуть историю транзакций");
            }
        }
    }
}