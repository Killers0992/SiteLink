namespace SiteLink.API.Networking
{
    public class SessionManager
    {
        public Dictionary<string, Session> Sessions { get; } = new Dictionary<string, Session>();    

        public void Update()
        {
            foreach(var session in Sessions.Values)
            {
                try
                {
                    session.Update();
                }
                catch (Exception ex)
                {
                    SiteLinkLogger.Error(ex);
                }
            }
        }

        public void CreateSession(Client client, Server server)
        {
            Session session = new Session(client, new Server[] { server });

            Sessions.Add(client.PreAuth.UserId, session);
        }

        public void CreateSession(Client client, Server[] servers)
        {
            Session session = new Session(client, servers);

            Sessions.Add(client.PreAuth.UserId, session);
        }
    }
}
