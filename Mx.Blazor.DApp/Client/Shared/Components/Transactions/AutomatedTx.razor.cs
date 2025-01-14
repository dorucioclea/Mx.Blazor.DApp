﻿using Microsoft.AspNetCore.Components;
using Mx.Blazor.DApp.Client.Application.Constants;
using Mx.Blazor.DApp.Client.Models;
using Mx.Blazor.DApp.Client.Services;
using Mx.NET.SDK.Domain.Data.Transaction;
using Mx.NET.SDK.Domain.Exceptions;

namespace Mx.Blazor.DApp.Client.Shared.Components.Transactions
{
    public partial class AutomatedTx
    {
        [Parameter]
        public TransactionModel TransactionModel { get; set; } = default!;

        [Parameter]
        public EventCallback<TransactionModel> Update { get; set; }

        [Parameter]
        public EventCallback<TransactionModel> Dismiss { get; set; }

        private readonly CancellationTokenSource SyncToken = new();

        protected override async Task OnInitializedAsync()
        {
            CancellationToken cancellationToken = SyncToken.Token;
            await Task.Factory.StartNew(async () =>
            {
                for (int i = 0; i < TransactionModel.Transactions.Count; i++)
                {
                    if (TransactionModel.Transactions[i].Status != "pending")
                        continue;

                    try
                    {
                        var transaction = Transaction.From(TransactionModel.Transactions[i].Hash);
                        await transaction.AwaitExecuted(MultiversxNetwork.Provider, MultiversxNetwork.TxCheckTime);
                        TransactionModel.Transactions[i].Status = transaction.Status;
                    }
                    catch (TransactionException.TransactionStatusNotReachedException)
                    {
                        TransactionModel.Transactions[i].Status = "fail";
                    }
                    catch (TransactionException.TransactionWithSmartContractErrorException)
                    {
                        TransactionModel.Transactions[i].Status = "fail";
                    }
                    catch (TransactionException.FailedTransactionException)
                    {
                        TransactionModel.Transactions[i].Status = "fail";
                    }
                    catch (TransactionException.InvalidTransactionException)
                    {
                        TransactionModel.Transactions[i].Status = "invalid";
                    }
                    catch (Exception)
                    {
                        TransactionModel.Transactions[i].Status = "exception";
                    }
                    finally
                    {
                        StateHasChanged();
                        await Update.InvokeAsync(TransactionModel);
                    }
                }
                TransactionsContainer.TransactionsExecuted(TransactionModel.Transactions.Select(t => t.Hash).ToArray());
                StateHasChanged();
            }, cancellationToken);
        }

        private async Task CopyToClipboard(string text)
        {
            await CopyService.CopyToClipboard(text);
        }
    }
}
