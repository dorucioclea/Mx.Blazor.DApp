﻿using Mx.Blazor.DApp.Client.Application.Helpers;
using Microsoft.JSInterop;
using static Mx.Blazor.DApp.Client.Application.Constants.BrowserStorage;

namespace Mx.Blazor.DApp.Client.Shared.Components.Login
{
    public partial class Login
    {
        private enum LedgerStates
        {
            List,
            Error,
            Verify,
            Loading
        };

        private bool isMobile = false;
        private bool extensionWalletAvailable = false;

        private string WalletConnectUri = "";

        private int LedgerAddressIndex { get; set; } = -1;
        private int pageNumber = 0;
        private const int pageSize = 10;
        private LedgerStates LedgerState;
        private string? AuthToken { get; set; }
        private List<string> Addresses { get; set; } = default!;

        protected override void OnInitialized()
        {
            isMobile = SessionStorage.GetItem<bool>(MOBILE_DEVICE);
            extensionWalletAvailable = SessionStorage.GetItem<bool>(EXTENSION_AVAILABLE);
        }

        public async void ExtensionWalletLogin()
        {
            await WalletProvider.ConnectToExtensionWallet();
        }

        public async void XPortalWalletLogin()
        {
            await WalletProvider.ConnectToXPortalWallet();
        }

        public void XPortalLogin()
        {
            NavigationManager.NavigateTo(WalletConnectUri);
        }

        public async void WebWalletLogin()
        {
            await WalletProvider.ConnectToWebWallet();
        }

        private void SetLedgerState(LedgerStates state)
        {
            LedgerState = state;
            StateHasChanged();
        }

        public async void HardwareWalletLogin()
        {
            pageNumber = 0;
            LedgerAddressIndex = -1;
            SetLedgerState(LedgerStates.Loading);
            Addresses = default!;

            var initialized = await JsRuntime.InvokeAsync<bool>("HardwareWallet.Obj.prepLedger");
            if (!initialized)
            {
                SetLedgerState(LedgerStates.Error);
                return;
            }

            await ReadLedgerAddresses(pageNumber, pageSize);
        }

        public async Task ReadLedgerAddresses(int pageNumber, int pageSize)
        {
            SetLedgerState(LedgerStates.Loading);

            Addresses = await JsRuntime.InvokeAsync<List<string>>("HardwareWallet.Obj.getLedgerAddresses", pageNumber, pageSize);

            SetLedgerState(LedgerStates.List);
        }

        public async void Prev()
        {
            if (pageNumber > 0)
            {
                LedgerAddressIndex = -1;
                pageNumber--;
                await ReadLedgerAddresses(pageNumber, pageSize);
            }
        }

        public async void Next()
        {
            if (Addresses != null)
            {
                LedgerAddressIndex = -1;
                pageNumber++;
                await ReadLedgerAddresses(pageNumber, pageSize);
            }
        }

        public void SetLedgerAddressIndex(int index)
        {
            LedgerAddressIndex = index + (pageNumber * pageSize);
            StateHasChanged();
        }

        public async void HardwareWalletConfirm()
        {
            if (LedgerAddressIndex == -1) return;

            AuthToken = GenerateAuthToken.Random();
            SetLedgerState(LedgerStates.Verify);
            await WalletProvider.ConnectToHardwareWallet(AuthToken);
            AuthToken = null;
        }
    }
}
