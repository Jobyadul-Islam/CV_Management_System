* If you choose database, consider using PostgreSQL instead of MySQL, the former is more often free than MySQL — e.g., Render provides PostgreSQL (if you use ORM – and you should – the migration is a very easy task).

Yes, data integrity is paramount. Unless there are very rare and very specific requirements, you shouldn't risk compromising it. So, what's about deletion? Sometimes it's acceptable to mark something as "deleted" instead of actually deleting it. But other times, that's not appropriate. The implementation of deletion really depends on the requirements, because there may be constraints — even legal ones — about whether data should be fully deleted at a user's request (e.g., for personal data protection) or, conversely, retained for a certain period (e.g., for tax or healthcare records). In our case, since there are no explicit requirements, you can choose the option that's easiest for you to implement. Depending on your approach, this could mean either cascade deletion or simply marking the data as deleted. Usually, cascade deletion in the database is the simplest—you can rely on the DB to maintain referential integrity and write 0 lines of additional code. Of course, you shouldn't reinvent the wheel here. Just set up cascade deletion in the database. Unfortunately, many students decide to write a lot of unnecessary code like “delete position, delete related comments, delete related CVs in a loop,” etc. This approach won't work well. First of all, it's too slow — but that's not the main problem. Imagine you've deleted the position and started deleting the linked CVs, and then a network hiccup occurs. Oops. Sure, you can make it work if you use transactions properly (which you'll need in other situation in the project), but it will be not very efficient (locking the access to multiple "main" tables at the same time) and why write code for something that the database can handle for you automagically? But yes, it's possible to mark deleted data as deleted and add additional code to handle it through layers, but I repeat: you may choose the simplest approach in this project.

Just to help with understanding. Attribute *names* are defined in globally (as well as attribute type). Attribute *values* are linked *to the Candidates*. A position is a subset of fields (and some other things, like project filter params). Category is just a field to filter/group attributes on UI, it doesn't influence your data model (of course, you store the category\_id in each attribute). E.g. a Recruiter can create a position with the title "Engineer" and add an attribute "Dancing Skills"— you don't try to "understand" and restrict this. Category doesn't do anything, it's used only for grouping and filtering in the list of attributes. It’s a value from a lookup table in the database (so it can be extended without code modifications; but you don’t need to create UI for editing/adding/deleting the categories). Any Recruiter can create an attribute, give it any name and select any category. Attributes don't have any links between each other. They are totally independent. 

Let's say a Recruiter creates two attributes, "Person Age" (numeric) and "Person Name" (string), and use them for the position "Generic Employee". Let's assume that these attributes gets attr\_007 and attr\_009 ids. Then some Candidate (user\_a) generates a CVs for that position with values 21 and "Mary". Then another Candidate (user\_b) generates a CVs for that position using values 32 and "John". Then the Recruiter edits the first numeric attribute to "Finger Amont", and adds a new string attributes "Address" to the position. After that USER\_C generates a CV with values values 10, "Ellen" and "Frisco". The CVs created before, of course, do not have values for the "Address" attribute, so opening the CVs will render that attribute as empty. So, your database contains something like the following (and other fields of course):  
user\_id | attribute\_id | value  
\--------+--------------+-------  
user\_a  | attr\_007     | 21  
\--------+--------------+-------  
user\_a  | attr\_009     | Mary  
\--------+--------------+-------  
user\_b  | attr\_007     | 32  
\--------+--------------+-------  
user\_b  | attr\_009     | John  
\--------+--------------+-------  
user\_c  | attr\_007     | 10  
\--------+--------------+-------  
user\_c  | attr\_009     | Ellen  
\--------+--------------+-------

user\_c  | attr\_010     | Frisco  
When you show CVs in lists for the given position, you display that users have 21 fingers on average (yes, *toes aren't fingers,* but *you* don't control the field names and their meaning). It's may seem strange, but it was the change made by the Recruiters, and you may simpy ignore this issue. Don't overcomplicate. 

A Recruiter configures a position so that only projects tagged with "Python" and "Data Engineering" are included in the generated CV, with a maximum of three projects. When a Candidate generates a CV for that position, only the three most recent projects matching those tags are included (from her/his projects).  
