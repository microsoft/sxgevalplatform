param serviceName string

param ipAddresses array

var ipObjects = [for (ip,i) in ipAddresses: {
    ipAddress: contains(ip, '/') ? ip : '${ip}/32'
    name: 'Allow-${serviceName}-${i}'
  }
]

output ips array = ipObjects
