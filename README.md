# SuperSharpShares 
## v0.1 Public Release
SuperSharpShares is a tool designed to automate enumerating domain shares, allowing for quick verification of accessible shares by your associated domain account.

## What is it?
SuperSharpShares came about somewhat unexpectedly - it was never intended for a public release and was not initially developed with that goal in mind. Originally, it served as a solution within a more intricate tool we created that remains unreleased (for now, never say never) to quickly enumerate available shares and perform other analyses. On internal pen tests and insider threats, we've had a lot of success with [Snaffler](https://github.com/SnaffCon/Snaffler) (with tuning for insider threats) but wanted something .net based that ran quickly to enumerate access control on shares as a standard user from a domain connected machine thus SuperSharpShares was born.



## Usage
SuperSharpShares takes no arguments and simply runs from a domain-connected machine or a runas session.
```
C:\Users\User\Desktop>SuperSharpShares.exe
Number of threads: 2

\\DC1\ADMIN$ - Read access
\\DC1\C$ - Read access
\\DC1\IT - Read access
\\DC1\NETLOGON - Read access
\\DC1\SYSVOL - Read access
\\DC1\Users - Read access
\\WS1\ADMIN$ - Read and Write access
\\WS1\Users - Read and Write access
\\WS1\C$ - Read and Write access
\\DC2\ADMIN$ - Read and Write access
\\DC2\NETLOGON - Read and Write access
\\DC2\SYSVOL - Read access
\\DC2\C$ - Read and Write access
```


## Blog Post
To read about how it works under the hood, how to modify it, and what it does, please check out the blog post!

- https://labs.lares.com/supersharpshares-release/
