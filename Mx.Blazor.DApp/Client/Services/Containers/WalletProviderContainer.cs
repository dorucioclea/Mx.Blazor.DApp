﻿using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Blazored.SessionStorage;
using Blazored.LocalStorage;
using static Mx.Blazor.DApp.Client.Application.Constants.BrowserStorage;
using Mx.Blazor.DApp.Client.Services.WalletProviders.Interfaces;
using Mx.Blazor.DApp.Client.Services.WalletProviders;
using Mx.Blazor.DApp.Client.Application.Constants;
using Mx.Blazor.DApp.Client.Application.ExtensionMethods;
using Mx.Blazor.DApp.Client.Application.Helpers;
using Mx.NET.SDK.Core.Domain.Helper;
using Mx.NET.SDK.Domain;
using Mx.NET.SDK.Provider.Dtos.API.Transactions;
using Mx.Blazor.DApp.Client.Application.Exceptions;
using Mx.Blazor.DApp.Shared.Connection;

namespace Mx.Blazor.DApp.Client.Services.Containers
{
    public class WalletProviderContainer
    {
        private readonly IHttpService Http;
        private readonly IJSRuntime JsRuntime;
        private readonly ISyncLocalStorageService _localStorage;
        private readonly ISyncSessionStorageService _sessionStorage;
        private readonly NavigationManager NavigationManager;
        private readonly TransactionsContainer TransactionsContainer;
        public WalletProviderContainer(
            IHttpService httpService,
            IJSRuntime jsRuntime,
            ISyncLocalStorageService localStorage,
            ISyncSessionStorageService sessionStorage,
            NavigationManager navigationManager,
            TransactionsContainer transactionsContainer)
        {
            Http = httpService;
            JsRuntime = jsRuntime;
            _sessionStorage = sessionStorage;
            _localStorage = localStorage;
            NavigationManager = navigationManager;
            TransactionsContainer = transactionsContainer;

            OnMaiarClientConnected += ValidateWalletConnection;
            OnMaiarClientDisconnected += WalletDisconnected;

            Initialize();
        }

        private IWalletProvider WalletProvider = default!;

        private static Func<AccountToken?, Task> OnMaiarClientConnected = default!;
        private static event Action? OnMaiarClientDisconnected;
        [JSInvokable]
        public static async void MaiarClientConnect(string accountInfo) => await OnMaiarClientConnected.Invoke(JsonWrapper.Deserialize<AccountToken>(accountInfo));
        [JSInvokable]
        public static void MaiarClientDisconnect() => OnMaiarClientDisconnected?.Invoke();

        private string? _authToken;

        public event Action? OnWalletConnected;
        public event Action? OnWalletDisconnected;
        public async Task ValidateWalletConnection(AccountToken? accountToken)
        {
            if (IsConnected()) return; //because Maiar wallet always validates at refresh
            if (accountToken == null || !accountToken.IsValid()) return;

            var connectionRequest = new ConnectionRequest()
            {
                AccountToken = accountToken,
                AuthToken = _authToken
            };
            _authToken = null;

            try
            {
                var connectionToken = await Http.PostAsync<ConnectionToken>("/connection/verify", connectionRequest);
                {
                    _sessionStorage.SetItemAsString(ACCESS_TOKEN, connectionToken.AccessToken);
                    _sessionStorage.SetItem(ACCOUNT_TOKEN, accountToken);

                    OnWalletConnected?.Invoke();
                }
            }
            catch //(HttpException hex) //access token could no be generated
            {
                await JsRuntime.InvokeVoidAsync("alert", "Access Token could not be generated");
            }
        }
        public void WalletDisconnected()
        {
            WalletProvider = default!;
            _sessionStorage.RemoveItem(ACCESS_TOKEN);
            _sessionStorage.RemoveItem(ACCOUNT_TOKEN);
            _sessionStorage.RemoveItem(WALLET_TYPE);
            _sessionStorage.RemoveItem(WEB_WALLET_STATE);
            _localStorage.RemoveAllWcItems();

            OnWalletDisconnected?.Invoke();
        }

        private void Initialize()
        {
            switch (_sessionStorage.GetItem<WalletType>(WALLET_TYPE))
            {
                case WalletType.Web:
                    WalletProvider = new WebWalletProvider(JsRuntime);
                    break;
                case WalletType.WalletConnect:
                    WalletProvider = new WalletConnectProvider(JsRuntime);
                    break;
                case WalletType.WalletConnectV2:
                    WalletProvider = new WalletConnectV2Provider(JsRuntime);
                    break;
                case WalletType.Extension:
                    WalletProvider = new ExtensionWalletProvider(JsRuntime);
                    break;
                case WalletType.Hardware:
                    WalletProvider = new HardwareWalletProvider(JsRuntime);
                    break;
                default:
                    _localStorage.RemoveAllWcItems();
                    break;
            }
        }

        public async Task InitializeAsync()
        {
            if (WalletProvider is null) return;

            switch (_sessionStorage.GetItem<WalletType>(WALLET_TYPE))
            {
                case WalletType.Web:
                    await WalletProvider.Init();
                    await WebWalletCheckingState();
                    break;
                case WalletType.WalletConnect:
                    await WalletProvider.Init();
                    break;
                case WalletType.WalletConnectV2:
                    await WalletProvider.Init();
                    break;
                case WalletType.Extension:
                    await WalletProvider.Init(_sessionStorage.GetItem<AccountToken>(ACCOUNT_TOKEN).Address);
                    break;
                case WalletType.Hardware:
                    await WalletProvider.Init();
                    break;
            }
        }

        public async Task ConnectToExtensionWallet()
        {
            WalletProvider = new ExtensionWalletProvider(JsRuntime);
            _authToken = GenerateAuthToken.Random();
            try
            {
                await WalletProvider.Init();
                var accountInfo = await WalletProvider.Login(_authToken);
                await ValidateWalletConnection(JsonWrapper.Deserialize<AccountToken>(accountInfo));
            }
            catch { }
        }

        public async Task ConnectToMaiarWallet()
        {
            WalletProvider = new WalletConnectProvider(JsRuntime);
            _authToken = GenerateAuthToken.Random();
            try
            {
                await WalletProvider.Init();
                await WalletProvider.Login(_authToken);
            }
            catch { }
        }

        public async Task ConnectToWalletV2()
        {
            WalletProvider = new WalletConnectV2Provider(JsRuntime);
            _authToken = GenerateAuthToken.Random();
            try
            {
                await WalletProvider.Init();
                await WalletProvider.Login(_authToken);
            }
            catch { }
        }

        public async Task ConnectToWebWallet()
        {
            WalletProvider = new WebWalletProvider(JsRuntime);
            _authToken = GenerateAuthToken.Random();
            try
            {
                await WalletProvider.Init();
                await WalletProvider.Login(_authToken);
            }
            catch { }
        }

        public async Task ConnectToHardwareWallet(string authToken)
        {
            WalletProvider = new HardwareWalletProvider(JsRuntime);
            _authToken = authToken;
            //Init is previously called from modal
            var accountInfo = await WalletProvider.Login(_authToken);
            await ValidateWalletConnection(JsonWrapper.Deserialize<AccountToken>(accountInfo));
        }

        public bool IsConnected()
        {
            if (WalletProvider is null) return false;

            try
            {
                var accountToken = _sessionStorage.GetItem<AccountToken>(ACCOUNT_TOKEN);
                return !string.IsNullOrEmpty(accountToken.Address);
            }
            catch { return false; }
        }

        public async Task Logout()
        {
            if (WalletProvider is null) return;


            await WalletProvider.Logout();
            WalletDisconnected();
        }

        public string MyAddress()
        {
            return _sessionStorage.GetItem<AccountToken>(ACCOUNT_TOKEN).Address;
        }

        public async Task SignTransaction(TransactionRequest transactionRequest, string title = "Transaction")
        {
            if (WalletProvider is null) return;

            if (_sessionStorage.GetItem<WalletType>(WALLET_TYPE) == WalletType.Web) _sessionStorage.SetItemAsString(TX_TITLE, title);

            var signedRequest = await WalletProvider.SignTransaction(transactionRequest);
            if (_sessionStorage.GetItem<WalletType>(WALLET_TYPE) == WalletType.Web) return;

            if (signedRequest == "canceled")
            {
                await WalletProvider.TransactionIsCanceled();
                return;
            }

            try
            {
                var transaction = JsonWrapper.Deserialize<TransactionRequestDto>(signedRequest);
                await SendTransaction(transaction, title);
            }
            catch { }
        }

        private async Task SendTransaction(TransactionRequestDto transaction, string title)
        {
            var response = await MultiversxNetwork.Provider.SendTransaction(transaction);
            TransactionsContainer.NewTransaction(title, response.TxHash);
            return;
        }

        public async Task SignTransactions(TransactionRequest[] transactionsRequest, string title = "Transactions")
        {
            if (WalletProvider is null) return;

            if (_sessionStorage.GetItem<WalletType>(WALLET_TYPE) == WalletType.Web) _sessionStorage.SetItemAsString(TX_TITLE, title);

            var signedRequests = await WalletProvider.SignTransactions(transactionsRequest);
            if (_sessionStorage.GetItem<WalletType>(WALLET_TYPE) == WalletType.Web) return;

            if (signedRequests == "canceled")
            {
                await WalletProvider.TransactionIsCanceled();
                return;
            }

            try
            {
                var transactions = JsonWrapper.Deserialize<TransactionRequestDto[]>(signedRequests);
                await SendTransactions(transactions, title);
            }
            catch { }

        }

        private async Task SendTransactions(TransactionRequestDto[] transactions, string title)
        {
            var response = await MultiversxNetwork.Provider.SendTransactions(transactions);
            TransactionsContainer.NewTransaction(title, response.TxsHashes.Select(tx => tx.Value).ToArray());
        }

        public async Task WebWalletCheckingState()
        {
            switch (_sessionStorage.GetItem<WebWalletState>(WEB_WALLET_STATE))
            {
                case WebWalletState.None:
                    break;
                case WebWalletState.LoggingIn:
                    _sessionStorage.SetItem(WEB_WALLET_STATE, WebWalletState.None);
                    var accountToken = NavigationManager.Uri.GetAccountTokenFromUrl();
                    _authToken = _sessionStorage.GetItemAsString(AUTH_TOKEN);
                    await ValidateWalletConnection(accountToken);
                    _sessionStorage.RemoveItem(AUTH_TOKEN);
                    NavigationManager.NavigateTo(NavigationManager.Uri.GetUrlWithoutParameters());
                    break;
                case WebWalletState.WaitingForTx:
                    var signedRequests = NavigationManager.Uri.GetTransactionsFromUrl();
                    NavigationManager.NavigateTo(NavigationManager.Uri.GetUrlWithoutParameters());
                    if (signedRequests == "canceled")
                    {
                        await WalletProvider.TransactionIsCanceled();
                        break;
                    }

                    try
                    {
                        var transactions = JsonWrapper.Deserialize<TransactionRequestDto[]>(signedRequests);
                        await SendTransactions(transactions, _sessionStorage.GetItemAsString(TX_TITLE));
                    }
                    catch { }
                    finally
                    {
                        _sessionStorage.RemoveItem(TX_TITLE);
                        _sessionStorage.SetItem(WEB_WALLET_STATE, WebWalletState.None);
                    }
                    break;
            }
        }
    }
}
