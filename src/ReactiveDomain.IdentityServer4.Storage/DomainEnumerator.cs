using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;

namespace ReactiveDomain.Identity.Storage
{
    /// <summary>
    /// The class provides the methods to enumerate domains. Methods defined in this class can be used in different projects which are using Elbe.
    /// </summary>
    public static class DomainEnumerator
    {        
        /// <summary>
        /// Purpose of this method is to return the avaiable domains
        /// </summary>
        public static List<string> GetDomains(List<string> domainsFromConfigFile)
        {           
            List<string> availableDomains = new List<string>
            {
                // Add local computer name by default.
                Environment.MachineName
            };
             availableDomains.AddRange(EnumerateDomains());
            if (domainsFromConfigFile != null && domainsFromConfigFile.Count > 0)
            {
                availableDomains.AddRange(domainsFromConfigFile);
            }
            return availableDomains;
        }        
        private static List<string> EnumerateDomains()
        {
            List<string> enumeratedDomains = new List<string>();            
            try
            {
                // Get domains in current forest and then for each of the domain get the domains it has a trust relationship with.
                Forest currentForest = Forest.GetCurrentForest();
                foreach (System.DirectoryServices.ActiveDirectory.Domain domain in currentForest.Domains)
                {
                    enumeratedDomains.Add(domain.Name);
                    foreach (TrustRelationshipInformation trust in domain.GetAllTrustRelationships())
                    {
                        enumeratedDomains.Add(trust.TargetName);
                    }
                }
            }
            catch (Exception)
            {

            }
            return enumeratedDomains;
        }
    }    
}

