using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace CDR.CaptureBehavior
{
    public class CustomBehavior : IServiceBehavior
    {
        #region IServiceBehavior Members

        /// <summary>
        /// Provides the ability to pass custom data to binding elements 
        /// to support the contract implementation.
        /// </summary>
        /// <param name="serviceDescription">The service description of the service.</param>
        /// <param name="serviceHostBase">The host of the service.</param>
        /// <param name="endpoints">The service endpoints.</param>
        /// <param name="bindingParameters">Custom objects to which binding elements have access.
        /// </param>
        public void AddBindingParameters(ServiceDescription serviceDescription, 
            System.ServiceModel.ServiceHostBase serviceHostBase, 
            System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, 
            System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
        {
            return;
        }

        /// <summary>
        /// Provides the ability to change run-time property values or 
        /// insert custom extension objects such as error handlers, 
        /// message or parameter interceptors, 
        /// security extensions, and other custom extension objects.
        /// </summary>
        /// <param name="serviceDescription">The service description.</param>
        /// <param name="serviceHostBase">The host that is currently being built.</param>
        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, 
            System.ServiceModel.ServiceHostBase serviceHostBase)
        {        
            foreach (ChannelDispatcher dispatcher in serviceHostBase.ChannelDispatchers)
            {
                dispatcher.Endpoints
                    .ToList()
                    .ForEach(x => x.DispatchRuntime.MessageInspectors.Add(new Inspector()));
                
            }
        }

        /// <summary>
        /// Provides the ability to inspect the service host and 
        /// the service description to confirm that the service 
        /// can run successfully.
        /// </summary>
        /// <param name="serviceDescription">The service description.</param>
        /// <param name="serviceHostBase">The service host that is currently being constructed.
        /// </param>
        public void Validate(ServiceDescription serviceDescription, 
            System.ServiceModel.ServiceHostBase serviceHostBase)
        {
            return;
        }

        #endregion
    }
}
