﻿using AutoMapper;
using Encryption;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.Responses.Negotiation;
using Nancy.Testing;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.Modules;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Login;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{
    [TestFixture]
    public class ConnectionModuleTest
    {
        private ConnectionModule _connectionModule;
        private IUserStore _userStore;
        private IConnectionValidator _connectionValidator;
        private IEncryptionProvider _encryptionProvider;
        private IWorkflowInfoService _workflowStoreService;
        private IWorkflowStoreFactory _workflowStoreFactory;

        [SetUp]
        public void ConnectionModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _encryptionProvider = Substitute.For<IEncryptionProvider>();
            _connectionValidator = Substitute.For<IConnectionValidator>();
            _workflowStoreService = Substitute.For<IWorkflowInfoService>();
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();

            _connectionModule = new ConnectionModule(_userStore, _connectionValidator, _encryptionProvider, _workflowStoreService, _workflowStoreFactory);

            Mapper.Initialize((cfg) =>
            {
                cfg.CreateMap<ConnectionModel, WorkflowStoreModel>();
            });

        }

        #region Delete Tests

        [Test]
        public void Delete_AuthTest()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            var connectionId = Guid.NewGuid();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId
            };
            _userStore.Connections.Returns(new List<ConnectionModel>() { connection });
            _userStore.GetConnection(connectionId).Returns(connection);

            foreach (string claim in Claims.AllClaims)
            {

                bootstrapper.CurrentUser.Claims = new string[] { claim };

                // execute
                var response = browser.Post(Actions.Connection.Delete, (with) =>
                {
                    with.HttpRequest();
                    with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                    with.FormValue("id", connectionId.ToString());
                });

                // assert
                if (claim == Claims.ConnectionDelete)
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
                else
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
                }
            }

        }

        [Test]
        public void Delete_NoConnectionFound_ReturnsNotFoundResponse()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser(false);
            var browser = new Browser(bootstrapper);
            var connectionId = Guid.NewGuid();

            _userStore.Connections.Returns(new List<ConnectionModel>());
            bootstrapper.CurrentUser.Claims = new string[] { Claims.ConnectionDelete };

            // execute
            var response = browser.Post(Actions.Connection.Delete, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

            _userStore.DidNotReceive().Save();
        }

        [Test]
        public void Delete_ConnectionFound_RemovesConnection()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            var connectionId = Guid.NewGuid();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId
            };
            List<ConnectionModel> connections = new List<ConnectionModel>();
            connections.Add(connection);
            bootstrapper.CurrentUser.Claims = new string[] { Claims.ConnectionDelete };

            _userStore.Connections.Returns(connections);
            _userStore.GetConnection(connectionId).Returns(connection);

            // execute
            var response = browser.Post(Actions.Connection.Delete, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            Assert.AreEqual(0, _userStore.Connections.Count);
            Assert.AreEqual(0, connections.Count);
            _userStore.Received(1).Save();
        }
        #endregion

        #region List Tests

        [Test]
        public void List_OnExecute_LoadsAllConnectionsForCurrentUser()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult("error"));

            int connectionCount = new Random().Next(3, 9);
            List<ConnectionModel> connections = new List<ConnectionModel>();
            for (var i = 0; i < connectionCount; i++)
            {
                connections.Add(new ConnectionModel());
            }
            _userStore.Connections.Returns(connections);

            // execute
            var response = browser.Get(Actions.Connection.List, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            _workflowStoreService.Received(connectionCount).PopulateWorkflowStoreInfo(Arg.Any<WorkflowStoreModel>());

        }

        #endregion

        #region Save Tests

        [Test]
        public void Save_AuthTest()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            var connectionId = Guid.NewGuid();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId
            };
            _userStore.Connections.Returns(new List<ConnectionModel>() { connection });
            _userStore.GetConnection(connectionId).Returns(connection);

            foreach (string claim in Claims.AllClaims)
            {
                _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult());

                bootstrapper.CurrentUser.Claims = new string[] { claim };

                // execute
                var response = browser.Post(Actions.Connection.Save, (with) =>
                {
                    with.HttpRequest();
                    with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                    with.FormValue("id", connectionId.ToString());
                });

                // assert
                if (claim == Claims.ConnectionAdd)
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
                else
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
                }
            }

        }

        [Test]
        public void Save_InvalidModel_ReturnsError()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            bootstrapper.CurrentUser.Claims = new string[] { Claims.ConnectionAdd };

            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult("error"));

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            _encryptionProvider.DidNotReceive().SimpleEncrypt(Arg.Any<string>(), Arg.Any<byte[]>(), null);
            _userStore.DidNotReceive().Save();
        }

        [Test]
        public void Save_NoPassword_DoesNotEncrypt()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            bootstrapper.CurrentUser.Claims = new string[] { Claims.ConnectionAdd };

            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult());
            _userStore.Connections.Returns(new List<ConnectionModel>());

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", "");
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
            _encryptionProvider.DidNotReceive().SimpleEncrypt(Arg.Any<string>(), Arg.Any<byte[]>(), null);
            _userStore.Received(1).Save();
        }

        [Test]
        public void Save_WithPassword_DoesEncryptAndSaves()
        {
            // setup
            byte[] key = new byte[20];
            new Random().NextBytes(key);
            string password = "testPassword";
            string encryptedPassword = Guid.NewGuid().ToString();

            var bootstrapper = this.ConfigureBootstrapperAndUser();
            bootstrapper.CurrentUser.Claims = new string[] { Claims.ConnectionAdd };

            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult());
            _encryptionProvider.NewKey().Returns(key);
            _encryptionProvider.SimpleEncrypt(password, key, null).Returns(encryptedPassword);

            List<ConnectionModel> connections = new List<ConnectionModel>();
            _userStore.Connections.Returns(connections);

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", password);
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
            _encryptionProvider.Received(1).SimpleEncrypt(password, key, null);

            Assert.AreEqual(1, _userStore.Connections.Count);
            Assert.AreEqual(encryptedPassword, _userStore.Connections[0].Password);
            _userStore.Received(1).Save();
        }


        #endregion

        #region Test Tests

        [Test]
        public void Test_InvalidModel_ReturnsError()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult("error"));

            // execute
            var response = browser.Post(Actions.Connection.Test, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            _workflowStoreFactory.DidNotReceive().GetWorkflowStore(Arg.Any<ConnectionModel>());
        }

        [Test]
        public void Test_ConnectionFails_ReturnsError()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult());

            // set up the workflow store to throw an exception
            IWorkflowStore store = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(Arg.Any<ConnectionModel>()).Returns(store);
            store.When(x => x.GetIncompleteCount()).Do(x => { throw new Exception("connection error"); });

            // execute
            var response = browser.Post(Actions.Connection.Test, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            _workflowStoreFactory.Received(1).GetWorkflowStore(Arg.Any<ConnectionModel>());
        }

        [Test]
        public void Test_ConnectionSucceeds_ReturnsSuccess()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapperAndUser();
            var browser = new Browser(bootstrapper);
            _connectionValidator.Validate(Arg.Any<ConnectionModel>()).Returns(new ValidationResult());

            // set up the workflow store to throw an exception
            IWorkflowStore store = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(Arg.Any<ConnectionModel>()).Returns(store);

            // execute
            var response = browser.Post(Actions.Connection.Test, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
            _workflowStoreFactory.Received(1).GetWorkflowStore(Arg.Any<ConnectionModel>());
            store.Received(1).GetIncompleteCount();
        }

        #endregion

        #region Private Methods

        private ModuleTestBootstrapper ConfigureBootstrapperAndUser(bool configureUsers = true)
        {
            var bootstrapper = new ModuleTestBootstrapper();
            bootstrapper.Login();
            bootstrapper.ConfigureRequestContainerCallback = (container) =>
            {
                container.Register<IUserStore>(_userStore);
                container.Register<IEncryptionProvider>(_encryptionProvider);
                container.Register<IConnectionValidator>(_connectionValidator);
                container.Register<IWorkflowInfoService>(_workflowStoreService);
                container.Register<IWorkflowStoreFactory>(_workflowStoreFactory);
            };

            if (configureUsers)
            {
                ConfigureUsers(bootstrapper);
            }
            return bootstrapper;
        }

        private List<UserModel> ConfigureUsers(ModuleTestBootstrapper bootstrapper)
        {
            // set up the logged in user
            UserModel user = new UserModel()
            {
                Id = bootstrapper.CurrentUser.Id,
                UserName = bootstrapper.CurrentUser.UserName,
                Role = Roles.User,
                Claims = new string[] { }
            };
            List<UserModel> users = new List<UserModel>() { user };
            _userStore.Users.Returns(users);
            return users;
        }

        #endregion


    }
}
