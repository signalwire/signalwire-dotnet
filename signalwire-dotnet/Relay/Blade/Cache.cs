using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Blade
{
    public sealed class Cache
    {
        public delegate void RouteAddCallback(UpstreamSession session, Route route);
        public delegate void RouteRemoveCallback(UpstreamSession session, string nodeID, Route route);
        public delegate void IdentityAddCallback(UpstreamSession session, Route route, string identity);
        public delegate void IdentityRemoveCallback(UpstreamSession session, string nodeID, Route route, string identity);
        public delegate void ProtocolAddCallback(UpstreamSession session, string protocol);
        public delegate void ProtocolRemoveCallback(UpstreamSession session, string protocol);
        public delegate void ProtocolProviderAddCallback(UpstreamSession session, Protocol protocol, Protocol.Provider provider);
        public delegate void ProtocolProviderRemoveCallback(UpstreamSession session, Protocol protocol, Protocol.Provider provider);
        public delegate void ProtocolProviderRankUpdateCallback(UpstreamSession session, Protocol protocol, int rank);
        public delegate void ProtocolProviderDataUpdateCallback(UpstreamSession session, Protocol protocol, Protocol.Provider provider);
        public delegate void ProtocolMethodAddCallback(UpstreamSession session, Protocol protocol, Protocol.Method method);
        public delegate void ProtocolMethodRemoveCallback(UpstreamSession session, Protocol protocol, Protocol.Method method);
        public delegate void ProtocolChannelAddCallback(UpstreamSession session, Protocol protocol, Protocol.Channel channel);
        public delegate void ProtocolChannelRemoveCallback(UpstreamSession session, Protocol protocol, Protocol.Channel channel);
        public delegate void SubscriptionAddCallback(UpstreamSession session, Subscription subscription);
        public delegate void SubscriptionRemoveCallback(UpstreamSession session, Subscription subscription);
        public delegate void AuthorityAddCallback(UpstreamSession session, Authority authority);
        public delegate void AuthorityRemoveCallback(UpstreamSession session, string nodeID, Authority authority);
        public delegate void AuthorizationAddCallback(UpstreamSession session, Authorization authorization);
        public delegate void AuthorizationRemoveCallback(UpstreamSession session, Authorization authorization);
        public delegate void AccessAddCallback(UpstreamSession session, Access access);
        public delegate void AccessRemoveCallback(UpstreamSession session, Access access);

        public sealed class Route
        {
            public string NodeID { get; internal set; }
            public IReadOnlyDictionary<string, bool> Identities { get { return InternalIdentities; } }

            internal ConcurrentDictionary<string, bool> InternalIdentities { get; set; } = new ConcurrentDictionary<string, bool>();
        }

        public sealed class Protocol
        {
            public sealed class Method
            {
                public string Name { get; internal set; }
                public Blade.Messages.AccessControl ExecuteAccess { get; internal set; }
            }

            public sealed class Channel
            {
                public string Name { get; internal set; }
                public Blade.Messages.AccessControl BroadcastAccess { get; internal set; }
                public Blade.Messages.AccessControl SubscribeAccess { get; internal set; }
            }
            
            public sealed class Provider
            {
                public string NodeID { get; internal set; }
                public JObject Data { get; internal set; }
                public int Rank { get; internal set; }
            }

            public string Name { get; internal set; }
            public Blade.Messages.AccessControl DefaultMethodExecuteAccess { get; internal set; }
            public Blade.Messages.AccessControl DefaultChannelBroadcastAccess { get; internal set; }
            public Blade.Messages.AccessControl DefaultChannelSubscribeAccess { get; internal set; }
            public IReadOnlyDictionary<string, Method> Methods { get { return InternalMethods; } }
            public IReadOnlyDictionary<string, Channel> Channels { get { return InternalChannels; } }
            public IReadOnlyDictionary<string, Provider> Providers { get { return InternalProviders; } }

            internal ConcurrentDictionary<string, Method> InternalMethods { get; set; } = new ConcurrentDictionary<string, Method>();
            internal ConcurrentDictionary<string, Channel> InternalChannels { get; set; } = new ConcurrentDictionary<string, Channel>();
            internal ConcurrentDictionary<string, Provider> InternalProviders { get; set; } = new ConcurrentDictionary<string, Provider>();
        }

        public sealed class Subscription
        {
            public string Protocol { get; internal set; }
            public string Channel { get; internal set; }
            public string NodeID { get; internal set; }
        }

        public sealed class Authority
        {
            public string NodeID { get; internal set; }
        }

        public sealed class Authorization
        {
            public string AuthenticationKey { get; internal set; }
            public JObject AuthorizationBlock { get; internal set; }
        }

        public sealed class Access
        {
            public string NodeID { get; internal set; }
            public string AuthenticationKey { get; internal set; }
        }


        private readonly UpstreamSession mSession = null;

        private ConcurrentDictionary<string, Route> mRoutes = new ConcurrentDictionary<string, Route>();
        private ConcurrentDictionary<string, Protocol> mProtocols = new ConcurrentDictionary<string, Protocol>();
        private ConcurrentDictionary<string, Subscription> mSubscriptions = new ConcurrentDictionary<string, Subscription>();
        private ConcurrentDictionary<string, Authority> mAuthorities = new ConcurrentDictionary<string, Authority>();
        private ConcurrentDictionary<string, Authorization> mAuthorizations = new ConcurrentDictionary<string, Authorization>();
        private ConcurrentDictionary<string, Access> mAccesses = new ConcurrentDictionary<string, Access>();
        // Temporary, remove when uncertified no longer need any netcasts
        private ConcurrentDictionary<string, bool> mProtocolsUncertified = new ConcurrentDictionary<string, bool>();

        public event RouteAddCallback OnRouteAdd;
        public event RouteRemoveCallback OnRouteRemove;
        public event IdentityAddCallback OnIdentityAdd;
        public event IdentityRemoveCallback OnIdentityRemove;
        public event ProtocolAddCallback OnProtocolAdd;
        public event ProtocolRemoveCallback OnProtocolRemove;
        public event ProtocolProviderAddCallback OnProtocolProviderAdd;
        public event ProtocolProviderRemoveCallback OnProtocolProviderRemove;
        public event ProtocolProviderRankUpdateCallback OnProtocolProviderRankUpdate;
        public event ProtocolProviderDataUpdateCallback OnProtocolProviderDataUpdate;
        public event ProtocolMethodAddCallback OnProtocolMethodAdd;
        public event ProtocolMethodRemoveCallback OnProtocolMethodRemove;
        public event ProtocolChannelAddCallback OnProtocolChannelAdd;
        public event ProtocolChannelRemoveCallback OnProtocolChannelRemove;
        public event SubscriptionAddCallback OnSubscriptionAdd;
        public event SubscriptionRemoveCallback OnSubscriptionRemove;
        public event AuthorityAddCallback OnAuthorityAdd;
        public event AuthorityRemoveCallback OnAuthorityRemove;
        public event AuthorizationAddCallback OnAuthorizationAdd;
        public event AuthorizationRemoveCallback OnAuthorizationRemove;
        public event AccessAddCallback OnAccessAdd;
        public event AccessRemoveCallback OnAccessRemove;

        public Cache(UpstreamSession session)
        {
            Logger = BladeLogging.CreateLogger<Cache>();
            mSession = session;
        }

        private ILogger Logger { get; set; }

        internal void Populate(Blade.Messages.ConnectResult connect)
        {
            mRoutes.Clear();
            mProtocols.Clear();
            mSubscriptions.Clear();
            mAuthorities.Clear();
            mAuthorizations.Clear();
            mAccesses.Clear();
            Logger.LogDebug("Cache cleared");

            connect.Routes?.ForEach(r => AddRoute(r.NodeID, r.Identities));
            connect.Protocols?.ForEach(proto =>
            {
                proto.Providers.ForEach(p => AddProtocolProvider(proto.Name, p.NodeID, p.Data as JObject, proto.DefaultMethodExecuteAccess, proto.DefaultChannelBroadcastAccess, proto.DefaultChannelSubscribeAccess, p.Rank));
                proto.Methods.ForEach(c => AddProtocolMethod(proto.Name, c.Name, c.ExecuteAccess));
                proto.Channels.ForEach(c => AddProtocolChannel(proto.Name, c.Name, c.BroadcastAccess, c.SubscribeAccess));
            });
            connect.Subscriptions?.ForEach(s => s.Subscribers?.ForEach(n => AddSubscription(s.Protocol, s.Channel, n)));
            connect.Authorities?.ForEach(a => AddAuthority(a));
            connect.Authorizations?.ForEach(a => AddAuthorization(a.Authentication, a.Authorization));
            connect.Accesses?.ForEach(a => AddAccess(a.NodeID, a.Authentication));
            connect.ProtocolsUncertified?.ForEach(p => AddProtocolUncertified(p));
        }

        internal void Update(Blade.Messages.NetcastParams netcast)
        {
            switch (netcast.Command)
            {
                case "route.add":
                    {
                        Blade.Messages.NetcastParams.RouteAddParam routeAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.RouteAddParam>();
                        AddRoute(routeAdd.NodeID, null);
                        break;
                    }
                case "route.remove":
                    {
                        Blade.Messages.NetcastParams.RouteRemoveParam routeRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.RouteRemoveParam>();
                        RemoveRoute(routeRemove.NodeID);
                        break;
                    }
                case "identity.add":
                    {
                        Blade.Messages.NetcastParams.IdentityAddParam identityAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.IdentityAddParam>();
                        AddIdentity(identityAdd.NodeID, identityAdd.Identity);
                        break;
                    }
                case "identity.remove":
                    {
                        Blade.Messages.NetcastParams.IdentityRemoveParam identityRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.IdentityRemoveParam>();
                        RemoveIdentity(identityRemove.NodeID, identityRemove.Identity);
                        break;
                    }
                case "protocol.add":
                    {
                        Blade.Messages.NetcastParams.ProtocolAddParam protocolAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolAddParam>();
                        AddProtocolUncertified(protocolAdd.Protocol);
                        break;
                    }
                case "protocol.remove":
                    {
                        Blade.Messages.NetcastParams.ProtocolRemoveParam protocolRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolRemoveParam>();
                        RemoveProtocolUncertified(protocolRemove.Protocol);
                        break;
                    }
                case "protocol.provider.add":
                    {
                        Blade.Messages.NetcastParams.ProtocolProviderAddParam protocolProviderAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolProviderAddParam>();
                        AddProtocolProvider(
                            protocolProviderAdd.Protocol,
                            protocolProviderAdd.NodeID, protocolProviderAdd.Data as JObject,
                            protocolProviderAdd.DefaultMethodExecuteAccess,
                            protocolProviderAdd.DefaultChannelBroadcastAccess,
                            protocolProviderAdd.DefaultChannelSubscribeAccess,
                            protocolProviderAdd.Rank);
                        protocolProviderAdd.Methods.ForEach(c => AddProtocolMethod(protocolProviderAdd.Protocol, c.Name, c.ExecuteAccess));
                        protocolProviderAdd.Channels.ForEach(c => AddProtocolChannel(protocolProviderAdd.Protocol, c.Name, c.BroadcastAccess, c.SubscribeAccess));
                        break;
                    }
                case "protocol.provider.remove":
                    {
                        Blade.Messages.NetcastParams.ProtocolProviderRemoveParam protocolProviderRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolProviderRemoveParam>();
                        RemoveProtocolProvider(protocolProviderRemove.Protocol, protocolProviderRemove.NodeID);
                        break;
                    }
                case "protocol.provider.rank.update":
                    {
                        Blade.Messages.NetcastParams.ProtocolProviderRankUpdateParam protocolProviderRankUpdate = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolProviderRankUpdateParam>();
                        UpdateProtocolProviderRank(protocolProviderRankUpdate.Protocol, protocolProviderRankUpdate.NodeID, protocolProviderRankUpdate.Rank);
                        break;
                    }
                case "protocol.provider.data.update":
                    {
                        Blade.Messages.NetcastParams.ProtocolProviderDataUpdateParam protocolProviderDataUpdate = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolProviderDataUpdateParam>();
                        UpdateProtocolProviderData(protocolProviderDataUpdate.Protocol, protocolProviderDataUpdate.NodeID, protocolProviderDataUpdate.Data as JObject);
                        break;
                    }
                case "protocol.method.add":
                    {
                        Blade.Messages.NetcastParams.ProtocolMethodAddParam protocolMethodAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolMethodAddParam>();
                        protocolMethodAdd.Methods.ForEach(c => AddProtocolMethod(protocolMethodAdd.Protocol, c.Name, c.ExecuteAccess));
                        break;
                    }
                case "protocol.method.remove":
                    {
                        Blade.Messages.NetcastParams.ProtocolMethodRemoveParam protocolMethodRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolMethodRemoveParam>();
                        protocolMethodRemove.Methods.ForEach(c => RemoveProtocolMethod(protocolMethodRemove.Protocol, c));
                        break;
                    }
                case "protocol.channel.add":
                    {
                        Blade.Messages.NetcastParams.ProtocolChannelAddParam protocolChannelAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolChannelAddParam>();
                        protocolChannelAdd.Channels.ForEach(c => AddProtocolChannel(protocolChannelAdd.Protocol, c.Name, c.BroadcastAccess, c.SubscribeAccess));
                        break;
                    }
                case "protocol.channel.remove":
                    {
                        Blade.Messages.NetcastParams.ProtocolChannelRemoveParam protocolChannelRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.ProtocolChannelRemoveParam>();
                        protocolChannelRemove.Channels.ForEach(c => RemoveProtocolChannel(protocolChannelRemove.Protocol, c));
                        break;
                    }
                case "subscription.add":
                    {
                        Blade.Messages.NetcastParams.SubscriptionAddParam subscriptionAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.SubscriptionAddParam>();
                        subscriptionAdd.Channels.ForEach(c => AddSubscription(subscriptionAdd.Protocol, c, subscriptionAdd.NodeID));
                        break;
                    }
                case "subscription.remove":
                    {
                        Blade.Messages.NetcastParams.SubscriptionRemoveParam subscriptionRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.SubscriptionRemoveParam>();
                        subscriptionRemove.Channels.ForEach(c => RemoveSubscription(subscriptionRemove.Protocol, c, subscriptionRemove.NodeID));
                        break;
                    }
                case "authority.add":
                    {
                        Blade.Messages.NetcastParams.AuthorityAddParam authorityAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.AuthorityAddParam>();
                        AddAuthority(authorityAdd.NodeID);
                        break;
                    }
                case "authority.remove":
                    {
                        Blade.Messages.NetcastParams.AuthorityRemoveParam authorityRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.AuthorityRemoveParam>();
                        RemoveAuthority(authorityRemove.NodeID);
                        break;
                    }
                case "authorization.add":
                    {
                        Blade.Messages.NetcastParams.AuthorizationAddParam authorizationAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.AuthorizationAddParam>();
                        AddAuthorization(authorizationAdd.Authentication, authorizationAdd.Authorization);
                        AddAccess(authorizationAdd.NodeID, authorizationAdd.Authentication);
                        break;
                    }
                case "authorization.remove":
                    {
                        Blade.Messages.NetcastParams.AuthorizationRemoveParam authorizationRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.AuthorizationRemoveParam>();
                        RemoveAuthorization(authorizationRemove.Authentication);
                        break;
                    }
                case "access.add":
                    {
                        Blade.Messages.NetcastParams.AccessAddParam accessAdd = netcast.ParametersAs<Blade.Messages.NetcastParams.AccessAddParam>();
                        AddAccess(accessAdd.NodeID, accessAdd.Authentication);
                        break;
                    }
                case "access.remove":
                    {
                        Blade.Messages.NetcastParams.AccessRemoveParam accessRemove = netcast.ParametersAs<Blade.Messages.NetcastParams.AccessRemoveParam>();
                        RemoveAccess(accessRemove.NodeID);
                        break;
                    }
                default:
                    Logger.LogWarning("Unhandled blade.netcast command '{0}'", netcast.Command);
                    break;
            }
        }

        private void AddRoute(string nodeid, List<string> identities)
        {
            Route route = new Route() { NodeID = nodeid };

            if (mRoutes.TryAdd(nodeid, route))
            {
                Logger.LogInformation("Route added '{0}'", nodeid);
                OnRouteAdd?.Invoke(mSession, route);
            }

            identities?.ForEach(i =>
            {
                if (route.InternalIdentities.TryAdd(i, true))
                {
                    Logger.LogInformation("Identity added '{0}' for '{1}'", i, nodeid);
                    OnIdentityAdd?.Invoke(mSession, route, i);
                }
            });
        }

        private void RemoveRoute(string nodeid)
        {
            mRoutes.TryRemove(nodeid, out Route route);
            Logger.LogInformation("Route removed '{0}'", nodeid);
            OnRouteRemove?.Invoke(mSession, nodeid, route);
            Array.ForEach(mProtocols.ToArray(), kv => RemoveProtocolProvider(kv.Value.Name, nodeid));
            RemoveAuthority(nodeid);
            Array.ForEach(mSubscriptions.ToArray(), kv => { if (kv.Value.NodeID == nodeid) RemoveSubscription(kv.Value.Protocol, kv.Value.Channel, nodeid); });
            RemoveAccess(nodeid);
        }


        private void AddIdentity(string nodeid, string identity)
        {
            if (mRoutes.TryGetValue(nodeid, out Route route))
            {
                if (route.InternalIdentities.TryAdd(identity, true))
                {
                    Logger.LogInformation("Identity added '{0}' for '{1}'", identity, nodeid);
                    OnIdentityAdd?.Invoke(mSession, route, identity);
                }
            }
        }

        private void RemoveIdentity(string nodeid, string identity)
        {
            if (mRoutes.TryGetValue(nodeid, out Route route))
            {
                route.InternalIdentities.TryRemove(identity, out bool unused);
            }
            Logger.LogInformation("Identity removed '{0}' from '{1}'", identity, nodeid);
            OnIdentityRemove?.Invoke(mSession, nodeid, route, identity);
        }

        public bool CheckRouteAvailable(string nodeid)
        {
            return mRoutes.TryGetValue(nodeid, out _);
        }

        private void AddProtocolUncertified(string protocol)
        {
            if (mProtocolsUncertified.TryAdd(protocol, true))
            {
                Logger.LogInformation("Protocol added '{0}'", protocol);
                OnProtocolAdd?.Invoke(mSession, protocol);
            }
        }

        private void RemoveProtocolUncertified(string protocol)
        {
            if (mProtocolsUncertified.TryRemove(protocol, out bool unused))
            {
                Logger.LogInformation("Protocol removed '{0}'", protocol);
                OnProtocolRemove?.Invoke(mSession, protocol);
            }
        }

        private void AddProtocolProvider(string protocol, string nodeid, JObject data, Blade.Messages.AccessControl default_method_execute_access, Blade.Messages.AccessControl default_channel_broadcast_access, Blade.Messages.AccessControl default_channel_subscribe_access, int rank)
        {
            // @note this is ugly, locking, but it deals with a race condition on protocol removal
            lock (mProtocols)
            {
                Protocol proto = mProtocols.GetOrAdd(protocol, s =>
                {
                    OnProtocolAdd?.Invoke(mSession, protocol);
                    Logger.LogInformation("Protocol added '{0}'", protocol);
                    return new Protocol()
                    {
                        Name = protocol,
                        DefaultMethodExecuteAccess = default_method_execute_access,
                        DefaultChannelBroadcastAccess = default_channel_broadcast_access,
                        DefaultChannelSubscribeAccess = default_channel_subscribe_access
                    };
                });

                Protocol.Provider provider = new Protocol.Provider() { NodeID = nodeid, Data = data, Rank = rank };
                if (proto.InternalProviders.TryAdd(nodeid, provider))
                {
                    Logger.LogInformation("Protocol provider added '{0}' to '{1}'", nodeid, protocol);
                    OnProtocolProviderAdd?.Invoke(mSession, proto, provider);
                }
            }
        }

        private void UpdateProtocolProviderRank(string protocol, string nodeid, int rank)
        {
            if (mProtocols.TryGetValue(protocol, out Protocol proto))
            {
                if (proto.InternalProviders.TryGetValue(nodeid, out Protocol.Provider provider))
                {
                    Logger.LogInformation("Protocol provider rank updated '{0}' from '{1}' to '{2}'", nodeid, protocol, rank);
                    provider.Rank = rank;
                    OnProtocolProviderRankUpdate?.Invoke(mSession, proto, rank);
                }
            }
        }

        private void UpdateProtocolProviderData(string protocol, string nodeid, JObject data)
        {
            if (mProtocols.TryGetValue(protocol, out Protocol proto))
            {
                if (proto.InternalProviders.TryGetValue(nodeid, out Protocol.Provider provider))
                {
                    Logger.LogInformation("Protocol provider data updated '{0}' from '{1}'", nodeid, protocol);
                    provider.Data = data;
                    OnProtocolProviderDataUpdate?.Invoke(mSession, proto, provider);
                }
            }
        }

        private void RemoveProtocolProvider(string protocol, string nodeid)
        {
            // @note this is ugly, locking, but it deals with a race condition on protocol removal
            lock (mProtocols)
            {
                if (mProtocols.TryGetValue(protocol, out Protocol proto))
                {
                    if (proto.InternalProviders.TryRemove(nodeid, out Protocol.Provider provider))
                    {
                        Logger.LogInformation("Protocol provider removed '{0}' from '{1}'", nodeid, protocol);
                        OnProtocolProviderRemove?.Invoke(mSession, proto, provider);

                        if (proto.InternalProviders.Count == 0)
                        {
                            // @note possible race condition here if we don't lock mProtocols on entire add/remove processes
                            if (mProtocols.TryRemove(protocol, out proto))
                            {
                                Logger.LogInformation("Protocol removed '{0}'", protocol);
                                OnProtocolRemove?.Invoke(mSession, protocol);
                            }
                        }
                    }
                }
            }
        }

        private void AddProtocolMethod(string protocol, string method, Blade.Messages.AccessControl execute_access)
        {
            if (mProtocols.TryGetValue(protocol, out Protocol proto))
            {
                Protocol.Method meth = new Protocol.Method() { Name = method, ExecuteAccess = execute_access };
                if (proto.InternalMethods.TryAdd(method, meth))
                {
                    Logger.LogInformation("Protocol method added '{0}' to '{1}'", method, protocol);
                    OnProtocolMethodAdd?.Invoke(mSession, proto, meth);
                }
            }
        }

        private void RemoveProtocolMethod(string protocol, string method)
        {
            if (mProtocols.TryGetValue(protocol, out Protocol proto))
            {
                if (proto.InternalMethods.TryRemove(method, out Protocol.Method meth))
                {
                    Logger.LogInformation("Protocol method removed '{0}' from '{1}'", method, protocol);
                    OnProtocolMethodRemove?.Invoke(mSession, proto, meth);
                }
            }
        }

        private void AddProtocolChannel(string protocol, string channel, Blade.Messages.AccessControl broadcast_access, Blade.Messages.AccessControl subscribe_access)
        {
            if (mProtocols.TryGetValue(protocol, out Protocol proto))
            {
                Protocol.Channel chan = new Protocol.Channel() { Name = channel, BroadcastAccess = broadcast_access, SubscribeAccess = subscribe_access };
                if (proto.InternalChannels.TryAdd(channel, chan))
                {
                    Logger.LogInformation("Protocol channel added '{0}' to '{1}'", channel, protocol);
                    OnProtocolChannelAdd?.Invoke(mSession, proto, chan);
                }
            }
        }

        private void RemoveProtocolChannel(string protocol, string channel)
        {
            if (mProtocols.TryGetValue(protocol, out Protocol proto))
            {
                if (proto.InternalChannels.TryRemove(channel, out Protocol.Channel chan))
                {
                    Logger.LogInformation("Protocol channel removed '{0}' from '{1}'", channel, protocol);
                    OnProtocolChannelRemove?.Invoke(mSession, proto, chan);
                }
            }
        }

        public bool CheckProtocolAvailable(string protocol)
        {
            if (mProtocols.TryGetValue(protocol, out Protocol proto)) return true;
            return mProtocolsUncertified.TryGetValue(protocol, out bool unused);
        }

        public Protocol.Provider GetRandomProtocolProvider(string protocol)
        {
            if (!mProtocols.TryGetValue(protocol, out Protocol proto)) return null;
            var providers = proto.InternalProviders.ToArray();
            if (providers.Length == 0) return null;
            return providers[new Random().Next(providers.Length - 1)].Value;
        }

        public List<Protocol> FindProtocols(Predicate<Protocol> predicate)
        {
            List<Protocol> protocols = new List<Protocol>();
            foreach (var kv in mProtocols)
            {
                if (predicate(kv.Value)) protocols.Add(kv.Value);
            }
            return protocols;
        }

        private void AddSubscription(string protocol, string channel, string nodeid)
        {
            Subscription subscription = new Subscription() { NodeID = nodeid, Protocol = protocol, Channel = channel };

            if (mSubscriptions.TryAdd(nodeid, subscription))
            {
                Logger.LogInformation("Subscription added '{0}' to channel '{1}' of protocol '{2}'", nodeid, channel, protocol);
                OnSubscriptionAdd?.Invoke(mSession, subscription);
            }
        }

        private void RemoveSubscription(string protocol, string channel, string nodeid)
        {
            if (mSubscriptions.TryRemove(nodeid, out Subscription subscription))
            {
                Logger.LogInformation("Subscription removed '{0}' from channel '{1}' of protocol '{2}'", nodeid, channel, protocol);
                OnSubscriptionRemove?.Invoke(mSession, subscription);
            }
        }


        private void AddAuthority(string nodeid)
        {
            Authority authority = new Authority() { NodeID = nodeid };

            if (mAuthorities.TryAdd(nodeid, authority))
            {
                Logger.LogInformation("Authority added '{0}'", nodeid);
                OnAuthorityAdd?.Invoke(mSession, authority);
            }
        }

        private void RemoveAuthority(string nodeid)
        {
            mAuthorities.TryRemove(nodeid, out Authority authority);
            Logger.LogInformation("Authority removed '{0}'", nodeid);
            OnAuthorityRemove?.Invoke(mSession, nodeid, authority);
        }

        public List<Authority> GetAuthorities()
        {
            return new List<Authority>(mAuthorities.Values);
        }

        public Authority GetRandomAuthority()
        {
            var authorities = mAuthorities.ToArray();
            if (authorities.Length == 0) return null;
            return authorities[new Random().Next(authorities.Length - 1)].Value;
        }


        private void AddAuthorization(string authentication, JObject authorization)
        {
            Authorization auth = new Authorization() {  AuthenticationKey = authentication, AuthorizationBlock = authorization };

            if (mAuthorizations.TryAdd(authentication, auth))
            {
                Logger.LogInformation("Authorization added '{0}'", authentication);
                OnAuthorizationAdd?.Invoke(mSession, auth);
            }
        }

        private void RemoveAuthorization(string authentication)
        {
            if (mAuthorizations.TryRemove(authentication, out Authorization authorization))
            {
                Logger.LogInformation("Authorization removed '{0}'", authentication);
                OnAuthorizationRemove?.Invoke(mSession, authorization);
            }
            Array.ForEach(mAccesses.ToArray(), kv => { if (kv.Value.AuthenticationKey == authentication) RemoveAccess(kv.Value.NodeID); });
        }


        private void AddAccess(string nodeid, string authentication)
        {
            Access access = new Access() { NodeID = nodeid, AuthenticationKey = authentication };

            if (mAccesses.TryAdd(nodeid, access))
            {
                Logger.LogInformation("Access added '{0}' for '{1}'", authentication, nodeid);
                OnAccessAdd?.Invoke(mSession, access);
            }
        }

        private void RemoveAccess(string nodeid)
        {
            if (mAccesses.TryRemove(nodeid, out Access access))
            {
                Logger.LogInformation("Access removed '{0}' from '{1}'", access.AuthenticationKey, nodeid);
                OnAccessRemove?.Invoke(mSession, access);
            }
        }
    }
}
