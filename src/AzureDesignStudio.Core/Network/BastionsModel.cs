﻿using AutoMapper;
using AzureDesignStudio.AzureResources.Base;
using AzureDesignStudio.AzureResources.Network;
using AzureDesignStudio.Core.Attributes;
using AzureDesignStudio.Core.Common;
using AzureDesignStudio.Core.DTO;
using AzureDesignStudio.Core.Models;
using Blazor.Diagrams.Core.Models;

namespace AzureDesignStudio.Core.Network
{
    [MapToDto(TypeKey = AdsConstants.Bastions)]
    public class BastionsModel : AzureNodeBase
    {
        public BastionsModel() : base()
        {
            AddPort(PortAlignment.Left);
            AddPort(PortAlignment.Top);
            AddPort(PortAlignment.Right);
            AddPort(PortAlignment.Bottom);
        }
        public override string ServiceName => "Azure Bastion";
        public override Type? DataFormType => typeof(BastionsForm);
        private readonly BastionHosts _bastionHost = new();
        protected override ResourceBase ArmResource => _bastionHost;
        public override (bool result, string message) IsDrappable(GroupModel overlappedGroup)
        {
            if (overlappedGroup is not SubnetModel subnet || !subnet.Name.Equals("AzureBastionSubnet"))
                return (false, "Azure Bastion needs a dedicat subnet: AzureBastionSubnet.");

            if (subnet.Children.Count > 0)
                return (false, "The subnet is not empty.");

            return (true, string.Empty);
        }
        public override bool IsValid => 
            Group is SubnetModel subnet && subnet.Children.Count == 1 && subnet.Name.Equals("AzureBastionSubnet");
        
        //[Required, DisplayName("Tier")]
        //public string Sku { get; set; } = "Standard";
        //[DisplayName("Instance")]
        //public int ScaleUnits { get; set; } = 2;
        public override AzureNodeDto GetNodeDto(IMapper mapper)
        {
            return mapper.Map<BastionsModelDto>(this);
        }

        protected override void PopulateArmAttributes()
        {
            base.PopulateArmAttributes();

            if (Group is not SubnetModel s)
                throw new Exception($"Bastion is not associated with a subnet.");

            // Depends on subnet is not enough. Must depend on vnet.
            if (s.Group is not VirtualNetworkModel vnet)
                throw new Exception($"Subnet must be in a vnet.");

            PublicIpModel? publicIp = null;
            foreach (var port in Ports)
            {
                publicIp = port.Links?.FirstOrDefault(l => l.SourcePort?.Parent is PublicIpModel)?.SourcePort?.Parent as PublicIpModel;
                if (publicIp != null)
                    break;
            }
            if (publicIp == null)
                throw new Exception($"Bastion has no public IP address.");

            BastionHostIPConfiguration ipConfig = new()
            {
                Name = Name + "-ipcfg",
                Properties = new()
                {
                    PublicIPAddress = new SubResource { Id = publicIp.ResourceId },
                    Subnet = new SubResource { Id = s.ResourceId }
                }
            };

            _bastionHost.Properties = new()
            {
                IpConfigurations = new List<BastionHostIPConfiguration> { ipConfig }
            };

            _bastionHost.DependsOn = new List<string>
            {
                vnet.ResourceId,
                s.ResourceId,
                publicIp.ResourceId,
            };
        }
    }
}
