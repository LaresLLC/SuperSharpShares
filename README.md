# SuperSharpShares

## What is it?
SuperSharpShares came about somewhat unexpectedly - it was never intended for a public release and was not initially developed with that goal in mind. Originally, it served as a solution within a more intricate tool we created that remains unreleased (for now, never say never) to quickly enumerate available shares and perform other analysis. On internal pentests and insider threats we've had a lot of success with [Snaffler](https://github.com/SnaffCon/Snaffler) (with tuning for insider threats) but wanted something .net based that ran quickly to enumerate access control on shares as a standard user from a domain connected machine thus SuperSharpShares was born.

SuperSharpShares was designed to automate the enumeration of domain shares. It helps you verify quickly which shares your associated domain account can access.

## Usage
SuperSharpShares takes no arguments and simply runs from a domain-connectedmachine  or runas session.
![image](https://github.com/LaresLLC/SuperSharpShares/assets/5783068/a153d2d1-263e-4e9c-bcfc-626ab3b8284c)


## Blog Post
To read about how it works under the hood, how to modify it and what it does please check out the blog post!

- https://labs.lares.com/supersharpshares-release/
