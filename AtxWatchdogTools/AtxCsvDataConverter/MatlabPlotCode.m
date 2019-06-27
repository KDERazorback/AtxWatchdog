data=csvread('datastream.csv', 1, 0);
v12=data(:,1);
v5=data(:,2);
v5sb=data(:,3);
v3_3=data(:,4);
hold on 
plot( v12, 'y' );
plot( v5, 'r' );
plot( v3_3, 'k');
plot( v5sb, 'm' );
grid
legend( 'V12', 'V5', 'V3.3', 'V5SB' );
hold off
print( 'datastream', '-dpng', '-r500' );