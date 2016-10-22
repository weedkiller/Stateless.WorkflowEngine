﻿using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using Nancy.ModelBinding;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Encryption;
using AutoMapper;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class ConnectionModule : WebConsoleSecureModule
    {
        private IUserStore _userStore;
        private IConnectionValidator _connectionValidator;
        private IEncryptionProvider _encryptionProvider;
        private IWorkflowInfoService _workflowStoreService;
        private IWorkflowStoreFactory _workflowStoreFactory;

        public ConnectionModule(IUserStore userStore, IConnectionValidator connectionValidator, IEncryptionProvider encryptionProvider, IWorkflowInfoService workflowStoreService, IWorkflowStoreFactory workflowStoreFactory)
            : base()
        {
            _userStore = userStore;
            _connectionValidator = connectionValidator;
            _encryptionProvider = encryptionProvider;
            _workflowStoreService = workflowStoreService;
            _workflowStoreFactory = workflowStoreFactory;

            // lists all connections for the current user
            Get[Actions.Connection.List] = (x) =>
            {
                this.RequiresAnyClaim(Roles.AllRoles);
                return this.List();
            };
            // deletes a connection for the current user
            Post[Actions.Connection.Delete] = (x) =>
            {
                this.RequiresAnyClaim(Roles.AllRoles);
                return DeleteConnection();
            };
            // saves a connection for the current user
            Post[Actions.Connection.Save] = (x) =>
            {
                this.RequiresAnyClaim(Roles.AllRoles);
                return Save();
            };
            // tests a new connection for the current user
            Post[Actions.Connection.Test] = (x) =>
            {
                this.RequiresAnyClaim(Roles.AllRoles);
                return Test();
            };
        }

        public dynamic DeleteConnection()
        {
            var id = Request.Form["id"];

            // load the connections for the current user
            var currentUser = _userStore.GetUser(this.Context.CurrentUser.UserName);

            var conn = currentUser.Connections.Where(x => x.Id == id).SingleOrDefault();
            if (conn == null)
            {
                var model = new { Error = "Connection not found" };
                return this.Response.AsJson(model, HttpStatusCode.NotFound);
            }

            currentUser.Connections.Remove(conn);
            _userStore.Save();
            return this.Response.AsJson(new { Result = "Ok" }, HttpStatusCode.OK);
        }

        public dynamic List()
        {
            // load the connections for the current user
            var currentUser = _userStore.GetUser(this.Context.CurrentUser.UserName);

            List<WorkflowStoreModel> workflowStoreModels = new List<WorkflowStoreModel>();
            foreach (ConnectionModel cm in currentUser.Connections) 
            {
                workflowStoreModels.Add(new WorkflowStoreModel(cm));
            }

            // process getting all the store info in parallel
            Parallel.ForEach(workflowStoreModels, _workflowStoreService.PopulateWorkflowStoreInfo);

            ConnectionListViewModel model = new ConnectionListViewModel();
            model.WorkflowStores.AddRange(workflowStoreModels);
            return this.View[Views.Connection.List, model]; ;

        }

        public dynamic Save()
        {
            var model = this.Bind<ConnectionModel>();
            var validationResult = _connectionValidator.Validate(model);

            if (!validationResult.Success)
            {
                return Response.AsJson<ValidationResult>(validationResult);
            }

            // encrypt the password if it's set
            if (!String.IsNullOrEmpty(model.Password))
            {
                byte[] key = _encryptionProvider.NewKey();
                model.Password = _encryptionProvider.SimpleEncrypt(model.Password, key);
            }

            // add the connection and return success
            var currentUser = _userStore.Users.Where(x => x.UserName == this.Context.CurrentUser.UserName).Single();
            currentUser.Connections.Add(model);
            _userStore.Save();

            return Response.AsJson<ValidationResult>(new ValidationResult());
        }

        public dynamic Test()
        {
            var model = this.Bind<ConnectionModel>();
            var validationResult = _connectionValidator.Validate(model);

            if (!validationResult.Success)
            {
                return Response.AsJson<ValidationResult>(validationResult);
            }

            // try and connect
            try
            {
                // create a store and try and get the active count - this will bomb out if there is a problem
                IWorkflowStore store = _workflowStoreFactory.GetWorkflowStore(model);
                store.GetIncompleteCount();

                // all good, return an empty validation result
                return Response.AsJson<ValidationResult>(new ValidationResult());
            }
            catch (Exception ex)
            {
                return Response.AsJson<ValidationResult>(new ValidationResult("Connection failed: " + ex.Message));
            }
        }


    }
}
